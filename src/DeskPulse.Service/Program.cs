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
                var command = (await reader.ReadLineAsync())?.Trim().ToUpperInvariant() ?? "";
                var response = HandleCommand(command);
                await writer.WriteLineAsync(response);
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
        switch (command)
        {
            case "STATUS": return _monitor.IsLoggingPaused
                ? "DeskPulse service is running. Logging is paused."
                : "DeskPulse service is running and monitoring activity.";
            case "LOGGING_STATE": return _monitor.IsLoggingPaused ? "PAUSED" : "ACTIVE";
            case "PAUSE_LOGGING": _monitor.PauseLogging(); return "OK|PAUSED";
            case "RESUME_LOGGING": _monitor.ResumeLogging(); return "OK|ACTIVE";
            case "RELOAD_SETTINGS": _monitor.ReloadSettings(); return "OK";
            case "TRAY_STARTED": _monitor.WriteUserEvent("DeskPulseTrayStarted", "DeskPulse started (possible login)", "DeskPulse tray application started; this may coincide with a Windows user login"); return "OK";
            case "TRAY_STOPPED": _monitor.WriteUserEvent("DeskPulseTrayStopped", "DeskPulse tray stopped", "DeskPulse tray application closed"); return "OK";
            default: return "Unknown command.";
        }
    }

    public void StartConsole() => OnStart(Array.Empty<string>());
    public void StopConsole() => OnStop();
}
