using System;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
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
    private readonly ServiceLoadTestController _loadTest = new();
    private ServiceSafetyMonitor? _safetyMonitor;
    private static readonly string CriticalPauseMarker = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DeskPulse", "critical-safety-pause.flag");

    public DeskPulseWindowsService()
    {
        ServiceName = "DeskPulse.Service";
        CanStop = true; CanShutdown = true; CanHandleSessionChangeEvent = true; AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        _monitor = new FileIoMonitor(false);
        _monitor.Start();

        var startupSettings = AppSettings.Load();
        if (File.Exists(CriticalPauseMarker))
        {
            if (startupSettings.PauseLoggingAtStartupAfterSafetyTrigger)
            {
                _monitor.PauseLogging();
            }
            else
            {
                // The critical condition paused the current run, but persistence was not requested.
                // Remove the stale marker so a service/Windows restart resumes logging normally.
                try { File.Delete(CriticalPauseMarker); } catch { }
            }
        }
        _safetyMonitor = new ServiceSafetyMonitor(WriteSafetyWarning, ActivateCriticalSafetyPause, () => _monitor?.IsLoggingPaused == true);
        _safetyMonitor.Start();
        _pipeCancellation = new CancellationTokenSource();
        _pipeTask = Task.Run(() => PipeLoopAsync(_pipeCancellation.Token));
    }

    protected override void OnStop()
    {
        _pipeCancellation?.Cancel();
        try { _pipeTask?.Wait(TimeSpan.FromSeconds(3)); } catch { }
        _safetyMonitor?.Dispose();
        _loadTest.Dispose();
        _monitor?.Dispose(); _monitor = null;
    }
    protected override void OnShutdown() => OnStop();

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        base.OnSessionChange(changeDescription);
        if (_monitor == null) return;
        if (changeDescription.Reason == System.ServiceProcess.SessionChangeReason.SessionLogon ||
            changeDescription.Reason == System.ServiceProcess.SessionChangeReason.ConsoleConnect ||
            changeDescription.Reason == System.ServiceProcess.SessionChangeReason.RemoteConnect)
        {
            _monitor.ReloadSettings();
        }
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
        if (mapped.HasValue) _monitor.HandleSessionSwitch(mapped.Value, changeDescription.SessionId);
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
                var clientProcessId = GetClientProcessId(pipe);
                var commandName = command.Split('|', 2)[0].Trim().ToUpperInvariant();
                var authorizationError = AuthorizeCommand(commandName, clientProcessId);
                if (authorizationError != null)
                {
                    await writer.WriteLineAsync(
                        commandName is "CLEAN_DATABASE_CURRENT_RULES" or
                            "SYSTEM_CLEAN_DATABASE_CURRENT_RULES" or
                            "REPAIR_HISTORICAL_DATA"
                            ? "RESULT|ERROR|" + authorizationError
                            : "ERROR|" + authorizationError);
                    continue;
                }
                if (command.Equals("CLEAN_DATABASE_CURRENT_RULES", StringComparison.OrdinalIgnoreCase))
                {
                    await RunDatabaseHousekeepingStreamingAsync(writer, clientProcessId, systemDatabase: false);
                }
                else if (command.Equals("SYSTEM_CLEAN_DATABASE_CURRENT_RULES", StringComparison.OrdinalIgnoreCase))
                {
                    await RunDatabaseHousekeepingStreamingAsync(writer, clientProcessId, systemDatabase: true);
                }
                else if (command.Equals("REPAIR_HISTORICAL_DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await RunHistoricalDataRepairStreamingAsync(writer, clientProcessId);
                }
                else
                {
                    var response = HandleCommand(command, clientProcessId);
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

    private static int GetClientProcessId(NamedPipeServerStream pipe)
    {
        if (NativeMethods.GetNamedPipeClientProcessId(
            pipe.SafePipeHandle,
            out var processId) &&
            processId <= int.MaxValue)
        {
            return checked((int)processId);
        }

        return 0;
    }

    private static int? GetProcessSessionId(int processId)
    {
        if (processId <= 0)
            return null;

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.SessionId;
        }
        catch
        {
            return null;
        }
    }

    private static class NativeMethods
    {
        internal const uint ProcessQueryLimitedInformation = 0x1000;
        internal const uint TokenQuery = 0x0008;
        internal const int TokenElevation = 20;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeClientProcessId(
            Microsoft.Win32.SafeHandles.SafePipeHandle pipe,
            out uint clientProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint processId);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out SafeAccessTokenHandle tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetTokenInformation(
            SafeAccessTokenHandle tokenHandle,
            int tokenInformationClass,
            out TokenElevationInfo tokenInformation,
            int tokenInformationLength,
            out int returnLength);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationInfo
    {
        public int TokenIsElevated;
    }

    private static string? AuthorizeCommand(string commandName, int clientProcessId)
    {
        var requiresKnownTray = commandName is
            "PAUSE_LOGGING" or "RESUME_LOGGING" or "RELOAD_SETTINGS" or
            "INSTALL_LIFECYCLE" or "START_LOAD_TEST" or "STOP_LOAD_TEST" or
            "DELETE_RECORDS" or "CLEAR_TABLE" or "CLEAR_ALL_RECORDS" or
            "SYSTEM_CLEAR_TABLE" or "SYSTEM_CLEAR_ALL_RECORDS" or
            "TRAY_STARTED" or "TRAY_STOPPED" or
            "CLEAN_DATABASE_CURRENT_RULES" or "SYSTEM_CLEAN_DATABASE_CURRENT_RULES" or
            "REPAIR_HISTORICAL_DATA";
        var requiresAdministrator = commandName is
            "START_LOAD_TEST" or "STOP_LOAD_TEST" or "REPAIR_HISTORICAL_DATA" or
            "SYSTEM_CLEAR_TABLE" or "SYSTEM_CLEAR_ALL_RECORDS" or
            "SYSTEM_CLEAN_DATABASE_CURRENT_RULES";

        if (!requiresKnownTray)
            return null;

        if (!TryGetClientSecurity(
            clientProcessId,
            out var executablePath,
            out var isAdministrator,
            out var isElevated))
        {
            return "The service could not verify the requesting process.";
        }

        if (!IsExpectedTrayExecutable(executablePath))
            return "The requesting executable is not the installed DeskPulse Tray.";

        if (requiresAdministrator && (!isAdministrator || !isElevated))
            return "This operation requires an elevated local administrator.";

        return null;
    }

    private static bool TryGetClientSecurity(
        int processId,
        out string executablePath,
        out bool isAdministrator,
        out bool isElevated)
    {
        executablePath = string.Empty;
        isAdministrator = false;
        isElevated = false;
        if (processId <= 0)
            return false;

        try
        {
            using (var process = Process.GetProcessById(processId))
                executablePath = process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return false;
        }

        var processHandle = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation,
            false,
            checked((uint)processId));
        if (processHandle == IntPtr.Zero)
            return false;

        try
        {
            if (!NativeMethods.OpenProcessToken(
                processHandle,
                NativeMethods.TokenQuery,
                out var tokenHandle))
            {
                return false;
            }

            using (tokenHandle)
            using (var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle()))
            {
                isAdministrator = identity.Groups?.Any(group =>
                    group is SecurityIdentifier sid &&
                    sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid)) == true;
                if (!NativeMethods.GetTokenInformation(
                    tokenHandle,
                    NativeMethods.TokenElevation,
                    out var elevation,
                    Marshal.SizeOf<TokenElevationInfo>(),
                    out _))
                {
                    return false;
                }

                isElevated = elevation.TokenIsElevated != 0;
                return !string.IsNullOrWhiteSpace(executablePath);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(processHandle);
        }
    }

    private static bool IsExpectedTrayExecutable(string executablePath)
    {
        try
        {
            var expectedPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "Tray",
                "DeskPulse.Tray.exe"));
            return string.Equals(
                Path.GetFullPath(executablePath),
                expectedPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string HandleCommand(string command, int clientProcessId)
    {
        if (_monitor == null) return "Service monitor is not running.";

        var commandName = command.Split('|', 2)[0].Trim().ToUpperInvariant();
        switch (commandName)
        {
            case "STATUS": return _monitor.IsLoggingPaused
                ? "DeskPulse service is running. Logging is paused."
                : "DeskPulse service is running and monitoring activity.";
            case "LOGGING_STATE": return File.Exists(CriticalPauseMarker) ? "CRITICAL_PAUSED" : (_monitor.IsLoggingPaused ? "PAUSED" : "ACTIVE");
            case "SAFETY_STATUS": return _safetyMonitor?.GetStatus() ?? "ERROR|Safety monitor unavailable.";
            case "PAUSE_LOGGING": _monitor.PauseLogging(); return "OK|PAUSED";
            case "RESUME_LOGGING":
                try { if (File.Exists(CriticalPauseMarker)) File.Delete(CriticalPauseMarker); } catch { }
                _safetyMonitor?.ClearCriticalState();
                _monitor.ResumeLogging(); return "OK|ACTIVE";
            case "RELOAD_SETTINGS": _monitor.ReloadSettingsForProcess(clientProcessId); return "OK";
            case "INSTALL_LIFECYCLE": return RecordInstallLifecycle(command);
            case "START_LOAD_TEST": return StartLoadTest(command);
            case "STOP_LOAD_TEST": return _loadTest.Stop(WriteLoadTestEvent);
            case "LOAD_TEST_STATUS": return _loadTest.GetStatus();
            case "DELETE_RECORDS": return DeleteSelectedRecords(command, clientProcessId);
            case "CLEAR_TABLE": return ClearTable(command, clientProcessId);
            case "CLEAR_ALL_RECORDS": return ClearAllRecords(clientProcessId);
            case "SYSTEM_CLEAR_TABLE": return ClearSystemTable(command);
            case "SYSTEM_CLEAR_ALL_RECORDS": return ClearAllSystemRecords();
            case "TRAY_STARTED": _monitor.WriteUserEvent("DeskPulseTrayStarted", "DeskPulse started (possible login)", "DeskPulse tray application started; this may coincide with a Windows user login", sessionId: GetProcessSessionId(clientProcessId)); return "OK";
            case "TRAY_STOPPED": _monitor.WriteUserEvent("DeskPulseTrayStopped", "DeskPulse tray stopped", "DeskPulse tray application closed", sessionId: GetProcessSessionId(clientProcessId)); return "OK";
            default: return "Unknown command.";
        }
    }


    private string RecordInstallLifecycle(string command)
    {
        var parts = command.Split('|', 5);
        if (parts.Length != 5)
            return "ERROR|Invalid installation lifecycle request.";

        var action = parts[1].Trim();
        var previousVersion = parts[2].Trim();
        var newVersion = parts[3].Trim();
        var installingUser = parts[4].Trim();

        string eventType;
        string eventDescription;
        string note;

        if (action.Equals("Installed", StringComparison.OrdinalIgnoreCase))
        {
            eventType = "DeskPulseInstalled";
            eventDescription = "DeskPulse installed";
            note = $"DeskPulse {newVersion} was installed.";
        }
        else if (action.Equals("Updated", StringComparison.OrdinalIgnoreCase))
        {
            eventType = "DeskPulseUpdated";
            eventDescription = "DeskPulse updated";
            note = $"DeskPulse was updated from version {previousVersion} to {newVersion}.";
        }
        else if (action.Equals("Reinstalled", StringComparison.OrdinalIgnoreCase))
        {
            eventType = "DeskPulseReinstalled";
            eventDescription = "DeskPulse reinstalled";
            note = $"DeskPulse {newVersion} was reinstalled.";
        }
        else
        {
            return "ERROR|Unknown installation lifecycle action.";
        }

        _monitor?.WriteUserEvent(eventType, eventDescription, note, installingUser, EventScope.System);
        return "OK";
    }

    private string StartLoadTest(string command)
    {
        var parts = command.Split('|');
        if (parts.Length != 4 ||
            !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cpuPercent) ||
            !double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var memoryPercent) ||
            !int.TryParse(parts[3], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var durationSeconds))
            return "ERROR|Invalid load-test request.";

        return _loadTest.Start(cpuPercent, memoryPercent, durationSeconds, WriteLoadTestEvent);
    }

    private void WriteLoadTestEvent(string note)
    {
        try
        {
            _monitor?.WriteUserEvent("DeskPulseDiagnosticLoadTest", "DeskPulse diagnostic load test", note, scope: EventScope.System);
        }
        catch
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-errors.log"), DateTime.Now + " " + note + Environment.NewLine); } catch { }
        }
    }


    private void WriteSafetyWarning(string note)
    {
        try { _monitor?.WriteUserEvent("DeskPulseServiceSafetyWarning", "DeskPulse service resource warning", note, scope: EventScope.System); } catch { }
    }

    private void ActivateCriticalSafetyPause(string note)
    {
        try
        {
            _monitor?.WriteUserEvent("DeskPulseServiceSafetyCritical", "DeskPulse service safety pause", note, scope: EventScope.System);
            Directory.CreateDirectory(Path.GetDirectoryName(CriticalPauseMarker)!);
            File.WriteAllText(CriticalPauseMarker, DateTimeOffset.UtcNow.ToString("O") + Environment.NewLine + note);
            _monitor?.PauseLogging();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-errors.log"), DateTime.Now + " Critical safety pause failed: " + ex + Environment.NewLine); } catch { }
        }
    }

    private string DeleteSelectedRecords(string command, int clientProcessId)
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

        return ExecuteDatabaseWrite(clientProcessId, database => database.DeleteRecordsByIds(tableName, ids));
    }

    private string ClearTable(string command, int clientProcessId)
    {
        var parts = command.Split('|', 2);
        if (parts.Length != 2)
            return "ERROR|Invalid clear-table request.";

        var tableName = NormalizeActivityTable(parts[1]);
        if (tableName == null)
            return "ERROR|Unknown activity table.";

        return ExecuteDatabaseWrite(clientProcessId, database => database.ClearTableRecords(tableName));
    }

    private string ClearAllRecords(int clientProcessId)
    {
        return ExecuteDatabaseWrite(clientProcessId, database => database.ClearAllRecords());
    }

    private string ClearSystemTable(string command)
    {
        var parts = command.Split('|', 2);
        if (parts.Length != 2)
            return "ERROR|Invalid system clear-table request.";
        var tableName = NormalizeActivityTable(parts[1]);
        if (tableName == null)
            return "ERROR|Unknown activity table.";
        return ExecuteSystemDatabaseWrite(database => database.ClearTableRecords(tableName));
    }

    private string ClearAllSystemRecords() =>
        ExecuteSystemDatabaseWrite(database => database.ClearAllRecords());

    private string ExecuteSystemDatabaseWrite(Func<DeskPulseDatabase, long> action)
    {
        if (_monitor == null)
            return "ERROR|Service monitor is not running.";
        try
        {
            var result = _monitor.ExecuteSystemDatabaseOperation(database =>
            {
                CreateSystemMaintenanceBackup(database);
                return action(database);
            });
            return "OK|" + result.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return "ERROR|" + SanitizePipeMessage(ex.Message);
        }
    }

    private static string CreateSystemMaintenanceBackup(DeskPulseDatabase database)
    {
        var backupFolder = Path.Combine(StorageLayout.SystemFolder, "Backups");
        var backupPath = Path.Combine(
            backupFolder,
            "DeskPulse-System-before-maintenance-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture) +
            ".db");
        database.BackupTo(backupPath);
        return backupPath;
    }

    private string ExecuteDatabaseWrite(int clientProcessId, Func<DeskPulseDatabase, long> action)
    {
        if (_monitor == null)
            return "ERROR|Service monitor is not running.";

        try
        {
            // Ordinary deletes and clears do not stop/restart ETW. They execute
            // against the monitor-owned database instance and therefore share
            // its database lock with live logging. This avoids the deadlock-like
            // wait previously caused by PauseLogging plus a second DB instance.
            var affected = _monitor.ExecuteDatabaseOperationForProcess(clientProcessId, action);
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


    private async Task RunHistoricalDataRepairStreamingAsync(StreamWriter writer, int clientProcessId)
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
                writer.WriteLine("PROGRESS|" + info.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + message);
        }

        try
        {
            SendProgress(new ExportProgressInfo(5, "5%   Windows service accepted historical data repair request"));
            if (!wasPaused)
                _monitor.PauseLogging();

            var progress = new InlineProgress<ExportProgressInfo>(SendProgress);
            var result = _monitor.ExecuteSystemDatabaseOperation(database =>
            {
                CreateSystemMaintenanceBackup(database);
                return database.RepairHistoricalData(progress, CancellationToken.None);
            });

            await writer.WriteLineAsync("RESULT|OK|" +
                result.ActivityRecordsRepaired.ToString(System.Globalization.CultureInfo.InvariantCulture));
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


    private async Task RunDatabaseHousekeepingStreamingAsync(
        StreamWriter writer,
        int clientProcessId,
        bool systemDatabase)
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

            var settings = systemDatabase
                ? AppSettings.LoadSystemSettings()
                : _monitor.GetSettingsForProcess(clientProcessId);
            var progress = new InlineProgress<ExportProgressInfo>(SendProgress);
            var result = systemDatabase
                ? _monitor.ExecuteSystemDatabaseOperation(database =>
                {
                    CreateSystemMaintenanceBackup(database);
                    return database.CleanDatabaseWithCurrentRules(settings, progress, CancellationToken.None);
                })
                : _monitor.ExecuteDatabaseOperationForProcess(clientProcessId, database =>
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
