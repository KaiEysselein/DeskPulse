using System;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace DeskPulse;

internal static class Program
{
    private static void Main(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();
        if (args.Any(a => a.Equals("--initialize-settings", StringComparison.OrdinalIgnoreCase)))
        {
            var settings = AppSettings.Load();
            settings.Save();
            Console.WriteLine("Shared DeskPulse settings initialized at " + settings.DataFolderPath);
            return;
        }
        if (Environment.UserInteractive || args.Any(a => a.Equals("--console", StringComparison.OrdinalIgnoreCase)))
        {
            using var service = new DeskPulseWindowsService();
            service.StartConsole();
            Console.WriteLine("DeskPulse service is running in console mode. Press Enter to stop.");
            Console.ReadLine();
            service.StopConsole();
            return;
        }
        ServiceBase.Run(new DeskPulseWindowsService());
    }
}

public sealed class DeskPulseWindowsService : ServiceBase
{
    private FileIoMonitor? _monitor;
    private CancellationTokenSource? _pipeCancellation;
    private Task? _pipeTask;

    public DeskPulseWindowsService()
    {
        ServiceName = "DeskPulse.Service";
        CanStop = true; CanShutdown = true; CanHandleSessionChangeEvent = true; AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        _monitor = new FileIoMonitor(false);
        _monitor.Start();
        _pipeCancellation = new CancellationTokenSource();
        _pipeTask = Task.Run(() => PipeLoopAsync(_pipeCancellation.Token));
    }

    protected override void OnStop()
    {
        _pipeCancellation?.Cancel();
        try { _pipeTask?.Wait(TimeSpan.FromSeconds(3)); } catch { }
        _monitor?.Dispose(); _monitor = null;
    }
    protected override void OnShutdown() => OnStop();

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        base.OnSessionChange(changeDescription);
        if (_monitor == null) return;
        var mapped = changeDescription.Reason switch
        {
            System.ServiceProcess.SessionChangeReason.SessionLogon => SessionSwitchReason.SessionLogon,
            System.ServiceProcess.SessionChangeReason.SessionLogoff => SessionSwitchReason.SessionLogoff,
            System.ServiceProcess.SessionChangeReason.SessionLock => SessionSwitchReason.SessionLock,
            System.ServiceProcess.SessionChangeReason.SessionUnlock => SessionSwitchReason.SessionUnlock,
            System.ServiceProcess.SessionChangeReason.ConsoleConnect => SessionSwitchReason.ConsoleConnect,
            System.ServiceProcess.SessionChangeReason.ConsoleDisconnect => SessionSwitchReason.ConsoleDisconnect,
            System.ServiceProcess.SessionChangeReason.RemoteConnect => SessionSwitchReason.RemoteConnect,
            System.ServiceProcess.SessionChangeReason.RemoteDisconnect => SessionSwitchReason.RemoteDisconnect,
            _ => (SessionSwitchReason?)null
        };
        if (mapped.HasValue) _monitor.HandleSessionSwitch(mapped.Value);
    }

    private async Task PipeLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = CreateServicePipe();
                await pipe.WaitForConnectionAsync(token);
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var command = (await reader.ReadLineAsync())?.Trim() ?? "";
                if (command.Equals("CLEAN_DATABASE_CURRENT_RULES", StringComparison.OrdinalIgnoreCase))
                {
                    await RunDatabaseHousekeepingStreamingAsync(writer);
                }
                else
                {
                    var response = HandleCommand(command);
                    await writer.WriteLineAsync(response);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-errors.log"), DateTime.Now + " " + ex + Environment.NewLine); } catch { } }
        }
    }


    private static NamedPipeServerStream CreateServicePipe()
    {
        // A Windows service normally creates named pipes with a security descriptor
        // that can exclude the interactive desktop user. Explicitly allow authenticated
        // local users to connect while retaining full control for Local System.
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            AppInfo.PipeName,
            PipeDirection.InOut,
            4,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private string HandleCommand(string command)
    {
        if (_monitor == null) return "Service monitor is not running.";

        var commandName = command.Split('|', 2)[0].Trim().ToUpperInvariant();
        switch (commandName)
        {
            case "STATUS": return _monitor.IsLoggingPaused
                ? "DeskPulse service is running. Logging is paused."
                : "DeskPulse service is running and monitoring activity.";
            case "LOGGING_STATE": return _monitor.IsLoggingPaused ? "PAUSED" : "ACTIVE";
            case "PAUSE_LOGGING": _monitor.PauseLogging(); return "OK|PAUSED";
            case "RESUME_LOGGING": _monitor.ResumeLogging(); return "OK|ACTIVE";
            case "RELOAD_SETTINGS": _monitor.ReloadSettings(); return "OK";
            case "DELETE_RECORDS": return DeleteSelectedRecords(command);
            case "CLEAR_TABLE": return ClearTable(command);
            case "CLEAR_ALL_RECORDS": return ClearAllRecords();
            case "TRAY_STARTED": _monitor.WriteUserEvent("DeskPulseTrayStarted", "DeskPulse started (possible login)", "DeskPulse tray application started; this may coincide with a Windows user login"); return "OK";
            case "TRAY_STOPPED": _monitor.WriteUserEvent("DeskPulseTrayStopped", "DeskPulse tray stopped", "DeskPulse tray application closed"); return "OK";
            default: return "Unknown command.";
        }
    }

    private string DeleteSelectedRecords(string command)
    {
        var parts = command.Split('|', 3);
        if (parts.Length != 3)
            return "ERROR|Invalid delete request.";

        var tableName = NormalizeActivityTable(parts[1]);
        if (tableName == null)
            return "ERROR|Unknown activity table.";

        var ids = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return "OK|0";

        return ExecuteDatabaseWrite(database => database.DeleteRecordsByIds(tableName, ids));
    }

    private string ClearTable(string command)
    {
        var parts = command.Split('|', 2);
        if (parts.Length != 2)
            return "ERROR|Invalid clear-table request.";

        var tableName = NormalizeActivityTable(parts[1]);
        if (tableName == null)
            return "ERROR|Unknown activity table.";

        return ExecuteDatabaseWrite(database => database.ClearTableRecords(tableName));
    }

    private string ClearAllRecords()
    {
        return ExecuteDatabaseWrite(database => database.ClearAllRecords());
    }

    private string ExecuteDatabaseWrite(Func<DeskPulseDatabase, long> action)
    {
        if (_monitor == null)
            return "ERROR|Service monitor is not running.";

        try
        {
            // Ordinary deletes and clears do not stop/restart ETW. They execute
            // against the monitor-owned database instance and therefore share
            // its database lock with live logging. This avoids the deadlock-like
            // wait previously caused by PauseLogging plus a second DB instance.
            var affected = _monitor.ExecuteDatabaseOperation(action);
            return "OK|" + affected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return "ERROR|" + SanitizePipeMessage(ex.Message);
        }
    }

    private static string? NormalizeActivityTable(string tableName)
    {
        return tableName.Trim().ToUpperInvariant() switch
        {
            "ACTIVITYEVENTS" => "ActivityEvents",
            "PROGRAMEVENTS" => "ProgramEvents",
            "USEREVENTS" => "UserEvents",
            _ => null
        };
    }

    private static string SanitizePipeMessage(string message) =>
        (message ?? "Unknown error").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

    private async Task RunDatabaseHousekeepingStreamingAsync(StreamWriter writer)
    {
        if (_monitor == null)
        {
            await writer.WriteLineAsync("RESULT|ERROR|Service monitor is not running.");
            return;
        }

        var wasPaused = _monitor.IsLoggingPaused;
        var writerLock = new object();

        void SendProgress(ExportProgressInfo info)
        {
            var message = SanitizePipeMessage(info.Message);
            lock (writerLock)
            {
                writer.WriteLine("PROGRESS|" + info.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + message);
            }
        }

        try
        {
            SendProgress(new ExportProgressInfo(15, "15%  Windows service accepted housekeeping request"));

            if (!wasPaused)
                _monitor.PauseLogging();

            var settings = AppSettings.Load();
            var progress = new InlineProgress<ExportProgressInfo>(SendProgress);
            var result = _monitor.ExecuteDatabaseOperation(database =>
                database.CleanDatabaseWithCurrentRules(settings, progress, CancellationToken.None));

            await writer.WriteLineAsync(string.Join("|",
                "RESULT",
                "OK",
                result.ActivityRecordsDeleted.ToString(System.Globalization.CultureInfo.InvariantCulture),
                result.ProgramRecordsDeleted.ToString(System.Globalization.CultureInfo.InvariantCulture),
                result.UserRecordsDeleted.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync("RESULT|ERROR|" + SanitizePipeMessage(ex.Message));
        }
        finally
        {
            if (!wasPaused)
                _monitor.ResumeLogging();
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;
        public InlineProgress(Action<T> report) => _report = report;
        public void Report(T value) => _report(value);
    }


    public void StartConsole() => OnStart(Array.Empty<string>());
    public void StopConsole() => OnStop();
}
