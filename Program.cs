using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

namespace DeskPulse;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppRuntime.Configure(args);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (AppRuntime.UninstallModeEnabled)
        {
            PortableUninstaller.RunInteractiveCleanup();
            return;
        }

        SQLitePCL.Batteries_V2.Init();

        Application.Run(new TrayAppContext());
    }
}

public static class AppInfo
{
    public const string AppName = "DeskPulse";
    public const string Version = "0.1.3.0";
    public const string GitHubUrl = "https://github.com/KaiEysselein/DeskPulse";
}

public static class AppRuntime
{
    public static bool DebugLoggingEnabled { get; private set; }
    public static bool DebugSkippedLoggingEnabled { get; private set; }
    public static bool MaintenanceModeEnabled { get; private set; }
    public static bool UninstallModeEnabled { get; private set; }

    public static void Configure(string[] args)
    {
        DebugLoggingEnabled = HasSwitch(args, "debug");
        DebugSkippedLoggingEnabled = HasSwitch(args, "debug-skipped");
        MaintenanceModeEnabled = HasSwitch(args, "maintenance") || HasSwitch(args, "m");
        UninstallModeEnabled = HasSwitch(args, "uninstall");
    }

    private static bool HasSwitch(string[] args, string switchName)
    {
        return args.Any(arg =>
            string.Equals(arg, "-" + switchName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--" + switchName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/" + switchName, StringComparison.OrdinalIgnoreCase));
    }
}

public static class DiagnosticLogger
{
    private static readonly object LogLock = new();

    public static string GetDiagnosticLogFilePath()
    {
        return Path.Combine(AppSettings.GetDefaultDataFolderPath(), "DeskPulse-diagnostics.log");
    }

    public static void WriteStartupEntry(AppSettings settings)
    {
        if (!AppRuntime.DebugLoggingEnabled)
            return;

        WriteLine("START",
            "DeskPulse diagnostic logging enabled. " +
            "Normal debug mode logs accepted monitored events and errors only. " +
            "Use -debug-skipped as an additional switch only when full skip-reason tracing is required.",
            settings);
    }

    public static void WriteFileDecision(
        string decision,
        string reason,
        string operation,
        string rawPath,
        string normalizedPath,
        string fullPath,
        string detectedExtension,
        string processName,
        int processId,
        AppSettings settings)
    {
        if (!AppRuntime.DebugLoggingEnabled)
            return;

        if (string.Equals(decision, "SKIP", StringComparison.OrdinalIgnoreCase) && !AppRuntime.DebugSkippedLoggingEnabled)
            return;

        var message =
            $"Decision={decision}; Reason={reason}; Operation={operation}; Process={processName}; ProcessId={processId}; " +
            $"DetectedExtension={detectedExtension}; RawPath={rawPath}; NormalizedPath={normalizedPath}; FullPath={fullPath}";

        WriteLine("FILE", message, settings);
    }

    public static void WriteException(string context, Exception ex)
    {
        if (!AppRuntime.DebugLoggingEnabled)
            return;

        try
        {
            var settings = AppSettings.Load();
            WriteLine("ERROR", context + Environment.NewLine + ex, settings);
        }
        catch
        {
            // Diagnostic logging must never crash DeskPulse.
        }
    }

    private static void WriteLine(string category, string message, AppSettings settings)
    {
        try
        {
            var logFilePath = Path.Combine(settings.DataFolderPath, "DeskPulse-diagnostics.log");
            Directory.CreateDirectory(settings.DataFolderPath);

            var activeExtensions = string.Join(", ", settings.ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            var line =
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                " | " + category +
                " | Debug=True" +
                " | DebugSkipped=" + AppRuntime.DebugSkippedLoggingEnabled +
                " | IgnoreTempFolders=" + settings.IgnoreTempFolders +
                " | ActiveExtensions=" + activeExtensions +
                " | " + message +
                Environment.NewLine;

            lock (LogLock)
            {
                File.AppendAllText(logFilePath, line);
            }
        }
        catch
        {
            // Diagnostic logging must never crash DeskPulse.
        }
    }
}

public static class PortableUninstaller
{
    public static void RunInteractiveCleanup()
    {
        try
        {
            var settings = AppSettings.Load();

            var confirm = MessageBox.Show(
                "This will remove DeskPulse current-user registry settings and generated log/report files.\n\n" +
                "The DeskPulse SQLite database will be preserved.\n\n" +
                "Files preserved:\n" +
                settings.DatabaseFilePath + "\n" +
                settings.DatabaseFilePath + "-wal\n" +
                settings.DatabaseFilePath + "-shm\n\n" +
                "Continue?",
                "DeskPulse Portable Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            var deletedFiles = new List<string>();
            var skippedFiles = new List<string>();

            DeleteFileIfExists(Path.Combine(Path.GetTempPath(), "DeskPulse-startup.log"), deletedFiles, skippedFiles);
            DeleteFileIfExists(DiagnosticLogger.GetDiagnosticLogFilePath(), deletedFiles, skippedFiles);
            DeleteFileIfExists(settings.ExcelExportFilePath, deletedFiles, skippedFiles);

            AppSettings.DeleteRegistrySettings();

            var message =
                "DeskPulse portable cleanup completed.\n\n" +
                "Removed current-user registry settings.\n" +
                "Preserved the SQLite data database.\n\n" +
                "Deleted files: " + deletedFiles.Count + "\n" +
                "Skipped files: " + skippedFiles.Count;

            if (skippedFiles.Count > 0)
                message += "\n\nSome files could not be deleted, usually because another program still has them open.";

            MessageBox.Show(
                message,
                "DeskPulse Portable Uninstall",
                MessageBoxButtons.OK,
                skippedFiles.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "DeskPulse portable cleanup could not be completed.\n\n" + ex.Message,
                "DeskPulse Portable Uninstall",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void DeleteFileIfExists(string filePath, List<string> deletedFiles, List<string> skippedFiles)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            File.Delete(filePath);
            deletedFiles.Add(filePath);
        }
        catch
        {
            skippedFiles.Add(filePath);
        }
    }
}

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _leftClickMenu;
    private readonly ContextMenuStrip _rightClickMenu;
    private readonly FileIoMonitor _monitor;

    public TrayAppContext()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = AppInfo.AppName,
            Visible = true
        };

        _monitor = new FileIoMonitor();

        _leftClickMenu = new ContextMenuStrip();
        _leftClickMenu.Items.Add("Export Activity Log", null, (_, _) => OpenExcelReport());
        _leftClickMenu.Items.Add("Settings...", null, (_, _) => OpenSettingsWindow());

        _rightClickMenu = new ContextMenuStrip();
        _rightClickMenu.Items.Add("About", null, (_, _) => OpenAboutWindow());

        if (AppRuntime.MaintenanceModeEnabled)
            _rightClickMenu.Items.Add("Maintenance...", null, (_, _) => OpenMaintenanceWindow());

        _rightClickMenu.Items.Add(new ToolStripSeparator());
        _rightClickMenu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon.ContextMenuStrip = _rightClickMenu;
        _trayIcon.MouseUp += OnTrayIconMouseUp;

        try
        {
            _monitor.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                $"{AppInfo.AppName} could not start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            ExitThread();
        }
    }


    private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _leftClickMenu.Show(Cursor.Position);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "file-logger.ico"),
                Path.Combine(Application.StartupPath, "file-logger.ico")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return new System.Drawing.Icon(candidate);
            }

            var executableIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            if (executableIcon != null)
                return executableIcon;
        }
        catch
        {
            // Fall back below.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void OpenExcelReport()
    {
        using var form = new ExportDateRangeForm((startDate, endDate, progress) =>
        {
            _monitor.ExportAndOpenExcelReport(startDate, endDate, progress);
        });

        form.ShowDialog();
    }

    private void OpenSettingsWindow()
    {
        using var form = new SettingsForm();

        if (form.ShowDialog() == DialogResult.OK)
        {
            _monitor.ReloadSettings();
        }
    }

    private void OpenMaintenanceWindow()
    {
        using var form = new MaintenanceForm();

        if (form.ShowDialog() == DialogResult.OK)
        {
            _monitor.ReloadSettings();
        }
    }

    private static void OpenAboutWindow()
    {
        using var form = new AboutForm();
        form.ShowDialog();
    }


    protected override void ExitThreadCore()
    {
        _monitor.Dispose();
        _trayIcon.MouseUp -= OnTrayIconMouseUp;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _leftClickMenu.Dispose();
        _rightClickMenu.Dispose();

        base.ExitThreadCore();
    }
}

public sealed class FileIoMonitor : IDisposable
{
    private const string SessionName = "DeskPulseSession";

    private readonly object _openFilesLock = new();
    private readonly object _settingsLock = new();
    private readonly Dictionary<string, List<FileOpenInfo>> _openEventsByFile = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;
    private DeskPulseDatabase _database;
    private ProgramActivityMonitor _programActivityMonitor;
    private TraceEventSession? _session;
    private Thread? _workerThread;
    private bool _sessionEventsSubscribed;
    private bool _disposed;

    public FileIoMonitor()
    {
        _settings = AppSettings.Load();

        EnsureDataFolderExists(_settings.DataFolderPath);

        _database = new DeskPulseDatabase(_settings.DatabaseFilePath);
        _database.Initialize();
        _programActivityMonitor = new ProgramActivityMonitor(GetSettingsSnapshot, GetDatabaseSnapshot);

        DiagnosticLogger.WriteStartupEntry(_settings);
        WriteUserEvent("AppStarted", "DeskPulse started", "Application startup completed");
        SubscribeSessionEvents();
    }

    private void SubscribeSessionEvents()
    {
        if (_sessionEventsSubscribed)
            return;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        _sessionEventsSubscribed = true;
    }

    private void UnsubscribeSessionEvents()
    {
        if (!_sessionEventsSubscribed)
            return;

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _sessionEventsSubscribed = false;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                WriteUserEvent("SessionLocked", "PC locked", "Windows session was locked");
                break;
            case SessionSwitchReason.SessionUnlock:
                WriteUserEvent("SessionUnlocked", "PC unlocked", "Windows session was unlocked");
                break;
            case SessionSwitchReason.SessionLogon:
                WriteUserEvent("SessionLogon", "User logged on", "Windows session logon detected");
                break;
            case SessionSwitchReason.SessionLogoff:
                WriteUserEvent("SessionLogoff", "User logging off", "Windows session logoff detected");
                break;
            case SessionSwitchReason.ConsoleConnect:
                WriteUserEvent("ConsoleConnect", "Console connected", "Console session connected");
                break;
            case SessionSwitchReason.ConsoleDisconnect:
                WriteUserEvent("ConsoleDisconnect", "Console disconnected", "Console session disconnected");
                break;
            case SessionSwitchReason.RemoteConnect:
                WriteUserEvent("RemoteConnect", "Remote session connected", "Remote session connected");
                break;
            case SessionSwitchReason.RemoteDisconnect:
                WriteUserEvent("RemoteDisconnect", "Remote session disconnected", "Remote session disconnected");
                break;
        }
    }

    private void WriteUserEvent(string eventType, string eventDescription, string note)
    {
        try
        {
            GetDatabaseSnapshot().InsertUserEvent(new UserEventRecord
            {
                EventTime = DateTime.Now,
                EventType = eventType,
                EventDescription = eventDescription,
                UserName = Environment.UserName,
                MachineName = Environment.MachineName,
                ProcessName = AppInfo.AppName,
                ProcessId = Environment.ProcessId,
                AppVersion = AppInfo.Version,
                Note = note
            });
        }
        catch (Exception ex)
        {
            TryWriteStartupDiagnostic(ex);
        }
    }

    public void Start()
    {
        if (TraceEventSession.IsElevated() != true)
        {
            throw new InvalidOperationException(
                "This program must be run as Administrator because Windows kernel ETW file I/O tracing requires elevation."
            );
        }

        _workerThread = new Thread(RunEtwSession)
        {
            IsBackground = true,
            Name = "DeskPulse ETW Monitor"
        };

        _workerThread.Start();
        _programActivityMonitor.Start();
    }

    public void ReloadSettings()
    {
        lock (_settingsLock)
        {
            _settings = AppSettings.Load();

            EnsureDataFolderExists(_settings.DataFolderPath);

            if (!string.Equals(_database.DatabaseFilePath, _settings.DatabaseFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _database.Dispose();
                _database = new DeskPulseDatabase(_settings.DatabaseFilePath);
            }

            _database.Initialize();
            _programActivityMonitor.ReloadSettings();

            DiagnosticLogger.WriteStartupEntry(_settings);
        }
    }

    public void ExportAndOpenExcelReport(DateTime startDate, DateTime endDate, IProgress<ExportProgressInfo>? progress = null)
    {
        AppSettings settings;
        DeskPulseDatabase database;

        lock (_settingsLock)
        {
            settings = _settings.Clone();
            database = _database;
        }

        EnsureDataFolderExists(settings.DataFolderPath);

        database.ExportToExcel(settings.ExcelExportFilePath, settings.ExportSheets, startDate, endDate, progress);

        progress?.Report(new ExportProgressInfo(98, "98%  Opening exported workbook"));

        Process.Start(new ProcessStartInfo
        {
            FileName = settings.ExcelExportFilePath,
            UseShellExecute = true
        });

        progress?.Report(new ExportProgressInfo(100, "100% Export complete"));
    }

    private AppSettings GetSettingsSnapshot()
    {
        lock (_settingsLock)
        {
            return _settings.Clone();
        }
    }

    private DeskPulseDatabase GetDatabaseSnapshot()
    {
        lock (_settingsLock)
        {
            return _database;
        }
    }

    private void RunEtwSession()
    {
        try
        {
            using var session = new TraceEventSession(SessionName)
            {
                StopOnDispose = true
            };

            _session = session;

            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.FileIO |
                KernelTraceEventParser.Keywords.FileIOInit
            );

            session.Source.Kernel.FileIOCreate += data => HandleFileEvent("OPEN", data);
            session.Source.Kernel.FileIOWrite += data => HandleFileEvent("WRITE", data);
            session.Source.Kernel.FileIOClose += data => HandleFileEvent("CLOSE", data);

            session.Source.Process();
        }
        catch (Exception ex)
        {
            TryWriteError(ex);
        }
    }

    private void HandleFileEvent(string operation, TraceEvent data)
    {
        var processId = data.ProcessID;
        var processName = string.IsNullOrWhiteSpace(data.ProcessName)
            ? "UnknownProcess"
            : data.ProcessName;

        if (IsDeskPulseProcess(processId, processName))
            return;

        var rawFileName = TryGetPayloadString(data, "FileName");

        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            LogFileDecision("SKIP", "ETW FileName payload was empty", operation, rawFileName ?? "", "", "", "", processName, processId);
            return;
        }

        var normalizedPath = PathUtilities.NormalizeEtwPath(rawFileName);

        if (!ShouldMonitorFile(rawFileName, normalizedPath, operation, processName, processId))
            return;

        var fullPath = GetSafeFullPath(normalizedPath);

        if (string.IsNullOrWhiteSpace(fullPath))
            return;

        var folderPath = PathUtilities.GetFolderPath(fullPath);
        var fileNameOnly = PathUtilities.GetFileNameOnly(fullPath);
        var extension = PathUtilities.GetExtension(fullPath);

        var key = BuildOpenFileKey(fullPath, processId, processName);
        var eventTime = DateTime.Now;

        if (operation == "OPEN")
        {
            var sizeAtOpening = TryGetFileSize(fullPath);

            RegisterOpenFileEvent(
                key,
                new FileOpenInfo
                {
                    OpenedTime = eventTime,
                    SizeAtOpening = sizeAtOpening
                }
            );

            WriteEvent(new ActivityEventRecord
            {
                ActivityType = "File",
                FullPath = fullPath,
                FolderPath = folderPath,
                FileName = fileNameOnly,
                Extension = extension,
                OpenedTime = eventTime,
                SizeAtOpening = sizeAtOpening,
                InferredAction = "Opened/read only",
                ProcessName = processName,
                ProcessId = processId,
                Note = "Open event logged immediately; close events may not contain a usable filename on all systems"
            });

            return;
        }

        if (operation == "WRITE")
        {
            var writeSize = TryGetFileSize(fullPath);
            var writeWasAttachedToOpenSession = RegisterFileWriteEvent(key, eventTime, writeSize);

            WriteEvent(new ActivityEventRecord
            {
                ActivityType = "File",
                FullPath = fullPath,
                FolderPath = folderPath,
                FileName = fileNameOnly,
                Extension = extension,
                FirstWriteTime = eventTime,
                LastWriteTime = eventTime,
                WriteCount = 1,
                SizeAtLastWrite = writeSize,
                InferredAction = "Save/write detected",
                ProcessName = processName,
                ProcessId = processId,
                Note = writeWasAttachedToOpenSession
                    ? "Write event logged immediately and matched to an open session"
                    : "Write event logged immediately, but matching open event was not found"
            });

            return;
        }

        if (operation == "CLOSE")
        {
            var closingSize = TryGetFileSize(fullPath);
            var openInfo = TryConsumeOpenFileEvent(key);

            WriteEvent(new ActivityEventRecord
            {
                ActivityType = "File",
                FullPath = fullPath,
                FolderPath = folderPath,
                FileName = fileNameOnly,
                Extension = extension,
                OpenedTime = openInfo?.OpenedTime,
                SizeAtOpening = openInfo?.SizeAtOpening,
                FirstWriteTime = openInfo?.FirstWriteTime,
                LastWriteTime = openInfo?.LastWriteTime,
                WriteCount = openInfo?.WriteCount ?? 0,
                SizeAtLastWrite = openInfo?.SizeAtLastWrite,
                ClosedTime = eventTime,
                SizeAtClosing = closingSize,
                InferredAction = openInfo == null
                    ? "Closed"
                    : InferFileAction(openInfo.SizeAtOpening, closingSize, openInfo.WriteCount),
                ProcessName = processName,
                ProcessId = processId,
                Note = openInfo == null
                    ? "Close event logged, but matching open event was not found"
                    : "Close event logged with matched open/write session"
            });
        }
    }

    private static bool IsDeskPulseProcess(int processId, string processName)
    {
        if (processId == Environment.ProcessId)
            return true;

        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var normalizedProcessName = processName.Trim();

        if (normalizedProcessName.Equals(AppInfo.AppName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalizedProcessName.Equals(AppInfo.AppName + ".exe", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalizedProcessName.Equals("DeskPulse", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalizedProcessName.Equals("DeskPulse.exe", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void WriteEvent(ActivityEventRecord record)
    {
        try
        {
            GetDatabaseSnapshot().InsertActivityEvent(record);
        }
        catch (Exception ex)
        {
            TryWriteStartupDiagnostic(ex);
        }
    }

    private void TryWriteError(Exception ex)
    {
        try
        {
            WriteEvent(new ActivityEventRecord
            {
                ActivityType = "Error",
                FullPath = "DeskPulse ETW session",
                FolderPath = "",
                FileName = "",
                Extension = "",
                InferredAction = "Monitor error",
                ProcessName = AppInfo.AppName,
                ProcessId = Environment.ProcessId,
                Note = ex.ToString()
            });
        }
        catch
        {
            TryWriteStartupDiagnostic(ex);
        }
    }

    private static void TryWriteStartupDiagnostic(Exception ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "DeskPulse-startup.log");

            File.AppendAllText(
                path,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + Environment.NewLine
                + ex
                + Environment.NewLine
                + Environment.NewLine
            );
        }
        catch
        {
            // Last-resort logging failed. Do not crash the monitor.
        }
    }

    private void RegisterOpenFileEvent(string key, FileOpenInfo openInfo)
    {
        lock (_openFilesLock)
        {
            if (!_openEventsByFile.TryGetValue(key, out var list))
            {
                list = new List<FileOpenInfo>();
                _openEventsByFile[key] = list;
            }

            list.Add(openInfo);
        }
    }

    private bool RegisterFileWriteEvent(string key, DateTime writeTime, long? writeSize)
    {
        lock (_openFilesLock)
        {
            if (!_openEventsByFile.TryGetValue(key, out var list))
                return false;

            if (list.Count == 0)
                return false;

            var openInfo = list[^1];
            openInfo.WriteCount++;

            if (openInfo.FirstWriteTime == null)
            {
                openInfo.FirstWriteTime = writeTime;
                openInfo.SizeAtFirstWrite = writeSize;
            }

            openInfo.LastWriteTime = writeTime;
            openInfo.SizeAtLastWrite = writeSize;

            return true;
        }
    }

    private FileOpenInfo? TryConsumeOpenFileEvent(string key)
    {
        lock (_openFilesLock)
        {
            if (!_openEventsByFile.TryGetValue(key, out var list))
                return null;

            if (list.Count == 0)
                return null;

            var openInfo = list[0];
            list.RemoveAt(0);

            if (list.Count == 0)
                _openEventsByFile.Remove(key);

            return openInfo;
        }
    }

    private bool ShouldMonitorFile(string rawFileName, string normalizedFileName, string operation, string processName, int processId)
    {
        var settings = GetSettingsSnapshot();
        var ext = PathUtilities.GetExtension(normalizedFileName);
        var fullPath = GetSafeFullPath(normalizedFileName) ?? "";

        try
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                LogFileDecision("SKIP", "Detected extension is empty", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            if (!settings.ExtensionsToMonitor.Contains(ext))
            {
                LogFileDecision("SKIP", "Detected extension is not in active monitored extensions", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                LogFileDecision("SKIP", "Full path is empty after normalization", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            if (settings.IgnoreTempFolders && PathExclusions.IsInTempFolder(fullPath))
            {
                LogFileDecision("SKIP", "File is inside a temporary folder and IgnoreTempFolders is enabled", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            if (LoggingRulesEngine.IsFileActivityExcluded(fullPath, processName, settings.LoggingRules))
            {
                LogFileDecision("SKIP", "File/process matched an excluded logging rule", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            var dbPath = Path.GetFullPath(settings.DatabaseFilePath);
            var exportPath = Path.GetFullPath(settings.ExcelExportFilePath);

            if (string.Equals(fullPath, dbPath, StringComparison.OrdinalIgnoreCase))
            {
                LogFileDecision("SKIP", "File is the DeskPulse SQLite database", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            if (string.Equals(fullPath, exportPath, StringComparison.OrdinalIgnoreCase))
            {
                LogFileDecision("SKIP", "File is the DeskPulse Excel export", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
                return false;
            }

            LogFileDecision("ACCEPT", "File passed monitoring filters", operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
            return true;
        }
        catch (Exception ex)
        {
            LogFileDecision("SKIP", "Exception while evaluating file: " + ex.Message, operation, rawFileName, normalizedFileName, fullPath, ext, processName, processId, settings);
            return false;
        }
    }

    private void LogFileDecision(string decision, string reason, string operation, string rawPath, string normalizedPath, string fullPath, string detectedExtension, string processName, int processId)
    {
        DiagnosticLogger.WriteFileDecision(
            decision,
            reason,
            operation,
            rawPath,
            normalizedPath,
            fullPath,
            detectedExtension,
            processName,
            processId,
            GetSettingsSnapshot());
    }

    private static void LogFileDecision(string decision, string reason, string operation, string rawPath, string normalizedPath, string fullPath, string detectedExtension, string processName, int processId, AppSettings settings)
    {
        DiagnosticLogger.WriteFileDecision(
            decision,
            reason,
            operation,
            rawPath,
            normalizedPath,
            fullPath,
            detectedExtension,
            processName,
            processId,
            settings);
    }

    private static string BuildOpenFileKey(string fullPath, int processId, string processName)
    {
        return $"{fullPath}|{processId}|{processName}";
    }

    private static string? TryGetPayloadString(TraceEvent data, string payloadName)
    {
        try
        {
            var value = data.PayloadByName(payloadName);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetSafeFullPath(string fileName)
    {
        try
        {
            return Path.GetFullPath(fileName);
        }
        catch
        {
            return fileName;
        }
    }

    private static long? TryGetFileSize(string fileName)
    {
        try
        {
            if (!File.Exists(fileName))
                return null;

            return new FileInfo(fileName).Length;
        }
        catch
        {
            return null;
        }
    }

    private static string InferFileAction(long? sizeAtOpening, long? sizeAtClosing, int writeCount)
    {
        if (writeCount > 0)
        {
            if (sizeAtOpening != null && sizeAtClosing != null && sizeAtOpening.Value != sizeAtClosing.Value)
                return "Edited/saved, file size changed";

            return "Edited/saved";
        }

        return "Opened/read only";
    }

    private static void EnsureDataFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new InvalidOperationException("The DeskPulse data folder path is empty.");

        Directory.CreateDirectory(folderPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            WriteUserEvent("AppStopped", "DeskPulse stopped", "Application shutdown started");
        }
        catch
        {
            // Ignore shutdown logging errors.
        }

        try
        {
            UnsubscribeSessionEvents();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        try
        {
            _programActivityMonitor.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        try
        {
            _session?.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        try
        {
            _database.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }
}

public sealed class ProgramActivityMonitor : IDisposable
{
    private readonly object _lock = new();
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<DeskPulseDatabase> _getDatabase;
    private readonly Dictionary<int, ProgramSnapshot> _knownProcesses = new();
    private System.Threading.Timer? _timer;
    private bool _disposed;
    private bool _isScanning;

    public ProgramActivityMonitor(Func<AppSettings> getSettings, Func<DeskPulseDatabase> getDatabase)
    {
        _getSettings = getSettings;
        _getDatabase = getDatabase;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _timer != null)
                return;

            if (!_getSettings().LogProgramActivity)
                return;

            _knownProcesses.Clear();

            foreach (var process in CaptureCurrentSessionProcesses())
                _knownProcesses[process.ProcessId] = process;

            _timer = new System.Threading.Timer(_ => ScanProcesses(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
    }

    public void ReloadSettings()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            if (_getSettings().LogProgramActivity)
            {
                if (_timer == null)
                {
                    _knownProcesses.Clear();

                    foreach (var process in CaptureCurrentSessionProcesses())
                        _knownProcesses[process.ProcessId] = process;

                    _timer = new System.Threading.Timer(_ => ScanProcesses(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
                }
            }
            else
            {
                _timer?.Dispose();
                _timer = null;
                _knownProcesses.Clear();
            }
        }
    }

    private void ScanProcesses()
    {
        if (_disposed)
            return;

        if (_isScanning)
            return;

        _isScanning = true;

        try
        {
            if (!_getSettings().LogProgramActivity)
                return;

            var currentProcesses = CaptureCurrentSessionProcesses()
                .ToDictionary(process => process.ProcessId);

            List<ProgramSnapshot> startedProcesses;
            List<ProgramSnapshot> stoppedProcesses;

            lock (_lock)
            {
                startedProcesses = currentProcesses
                    .Where(pair => !_knownProcesses.ContainsKey(pair.Key))
                    .Select(pair => pair.Value)
                    .ToList();

                stoppedProcesses = _knownProcesses
                    .Where(pair => !currentProcesses.ContainsKey(pair.Key))
                    .Select(pair => pair.Value)
                    .ToList();

                _knownProcesses.Clear();

                foreach (var pair in currentProcesses)
                    _knownProcesses[pair.Key] = pair.Value;
            }

            var settings = _getSettings();

            foreach (var process in startedProcesses)
            {
                if (!LoggingRulesEngine.IsProgramActivityExcluded(process.FilePath, process.ProcessName, settings.LoggingRules))
                    WriteProgramEvent("ProgramStarted", "Program started", process, process.StartTime ?? DateTime.Now, "Detected in the current interactive Windows session");
            }

            foreach (var process in stoppedProcesses)
            {
                if (!LoggingRulesEngine.IsProgramActivityExcluded(process.FilePath, process.ProcessName, settings.LoggingRules))
                    WriteProgramEvent("ProgramStopped", "Program closed", process, DateTime.Now, "Process no longer detected in the current interactive Windows session");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.WriteException("Program activity scan failed", ex);
        }
        finally
        {
            _isScanning = false;
        }
    }

    private static List<ProgramSnapshot> CaptureCurrentSessionProcesses()
    {
        var result = new List<ProgramSnapshot>();
        var currentSessionId = Process.GetCurrentProcess().SessionId;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId)
                    continue;

                if (process.SessionId != currentSessionId)
                    continue;

                var processName = SafeGetProcessName(process);

                if (string.IsNullOrWhiteSpace(processName))
                    continue;

                result.Add(new ProgramSnapshot
                {
                    ProcessId = process.Id,
                    ProcessName = processName,
                    FilePath = SafeGetMainModuleFileName(process),
                    WindowTitle = SafeGetMainWindowTitle(process),
                    StartTime = SafeGetStartTime(process),
                    UserName = Environment.UserName,
                    MachineName = Environment.MachineName
                });
            }
            catch
            {
                // Some protected/system processes cannot be inspected. Skip them quietly.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    private void WriteProgramEvent(string eventType, string eventDescription, ProgramSnapshot snapshot, DateTime eventTime, string note)
    {
        try
        {
            _getDatabase().InsertProgramEvent(new ProgramEventRecord
            {
                EventTime = eventTime,
                EventType = eventType,
                EventDescription = eventDescription,
                ProgramName = snapshot.ProcessName,
                ProcessId = snapshot.ProcessId,
                FilePath = snapshot.FilePath,
                WindowTitle = snapshot.WindowTitle,
                UserName = snapshot.UserName,
                MachineName = snapshot.MachineName,
                AppVersion = AppInfo.Version,
                Note = note
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogger.WriteException("Program activity event could not be written", ex);
        }
    }

    private static string SafeGetProcessName(Process process)
    {
        try { return process.ProcessName ?? ""; }
        catch { return ""; }
    }

    private static string SafeGetMainModuleFileName(Process process)
    {
        try { return process.MainModule?.FileName ?? ""; }
        catch { return ""; }
    }

    private static string SafeGetMainWindowTitle(Process process)
    {
        try { return process.MainWindowTitle ?? ""; }
        catch { return ""; }
    }

    private static DateTime? SafeGetStartTime(Process process)
    {
        try { return process.StartTime; }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}

public sealed class ProgramSnapshot
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public DateTime? StartTime { get; set; }
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
}

public sealed class ExportProgressInfo
{
    public ExportProgressInfo(int percent, string message)
    {
        Percent = Math.Max(0, Math.Min(100, percent));
        Message = message;
    }

    public int Percent { get; }
    public string Message { get; }
}

public sealed class ExcelExportProgressTracker
{
    private readonly IProgress<ExportProgressInfo>? _progress;
    private readonly int _totalRowsToWrite;
    private int _rowsWritten;
    private int _lastPercent;

    public ExcelExportProgressTracker(IProgress<ExportProgressInfo>? progress, int totalRowsToWrite)
    {
        _progress = progress;
        _totalRowsToWrite = Math.Max(1, totalRowsToWrite);
        Report(5, "5%   Writing worksheet rows");
    }

    public void ReportRowsWritten(int rowCount, string worksheetName)
    {
        _rowsWritten += Math.Max(0, rowCount);

        var rowProgress = (double)_rowsWritten / _totalRowsToWrite;
        var percent = 5 + (int)Math.Round(rowProgress * 88D, MidpointRounding.AwayFromZero);

        Report(percent, $"{percent}%   Writing worksheet rows ({worksheetName}: {Math.Min(_rowsWritten, _totalRowsToWrite)} of {_totalRowsToWrite} rows)");
    }

    public void ReportWorksheetComplete(string worksheetName)
    {
        if (_rowsWritten == 0)
            Report(10, $"10%  Writing worksheet rows ({worksheetName}: 0 rows for selected date range)");
    }

    private void Report(int percent, string message)
    {
        percent = Math.Max(_lastPercent, Math.Min(93, percent));
        _lastPercent = percent;
        _progress?.Report(new ExportProgressInfo(percent, message));
    }
}

public sealed class DeskPulseDatabase : IDisposable
{
    private readonly object _dbLock = new();

    public DeskPulseDatabase(string databaseFilePath)
    {
        DatabaseFilePath = databaseFilePath;
    }

    public string DatabaseFilePath { get; }

    private string ConnectionString
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabaseFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            return builder.ToString();
        }
    }

    public void Initialize()
    {
        lock (_dbLock)
        {
            var folder = Path.GetDirectoryName(DatabaseFilePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA journal_mode=WAL;");
            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            ExecuteNonQuery(connection,
                """
                CREATE TABLE IF NOT EXISTS ActivityEvents
                (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CreatedAt TEXT NOT NULL,
                    ActivityType TEXT NOT NULL,
                    Item TEXT NOT NULL,
                    FullPath TEXT NULL,
                    FolderPath TEXT NULL,
                    FileName TEXT NULL,
                    Extension TEXT NULL,
                    DateOpened TEXT NULL,
                    TimeOpened TEXT NULL,
                    SizeAtOpening INTEGER NULL,
                    FirstWriteDate TEXT NULL,
                    FirstWriteTime TEXT NULL,
                    LastWriteDate TEXT NULL,
                    LastWriteTime TEXT NULL,
                    WriteCount INTEGER NULL,
                    SizeAtLastWrite INTEGER NULL,
                    DateClosed TEXT NULL,
                    TimeClosed TEXT NULL,
                    SizeAtClosing INTEGER NULL,
                    InferredAction TEXT NULL,
                    ProcessName TEXT NULL,
                    ProcessId INTEGER NULL,
                    Note TEXT NULL
                );
                """);

            EnsureColumnExists(connection, "ActivityEvents", "FullPath", "TEXT NULL");
            EnsureColumnExists(connection, "ActivityEvents", "FolderPath", "TEXT NULL");
            EnsureColumnExists(connection, "ActivityEvents", "FileName", "TEXT NULL");
            EnsureColumnExists(connection, "ActivityEvents", "Extension", "TEXT NULL");

            ExecuteNonQuery(connection,
                """
                CREATE TABLE IF NOT EXISTS UserEvents
                (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CreatedAt TEXT NOT NULL,
                    EventDate TEXT NULL,
                    EventTime TEXT NULL,
                    EventType TEXT NOT NULL,
                    EventDescription TEXT NULL,
                    UserName TEXT NULL,
                    MachineName TEXT NULL,
                    ProcessName TEXT NULL,
                    ProcessId INTEGER NULL,
                    AppVersion TEXT NULL,
                    Note TEXT NULL
                );
                """);

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_CreatedAt ON ActivityEvents (CreatedAt);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_Item ON ActivityEvents (Item);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_FullPath ON ActivityEvents (FullPath);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_Extension ON ActivityEvents (Extension);"
            );

            ExecuteNonQuery(connection,
                """
                CREATE TABLE IF NOT EXISTS ProgramEvents
                (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CreatedAt TEXT NOT NULL,
                    EventDate TEXT NULL,
                    EventTime TEXT NULL,
                    EventType TEXT NOT NULL,
                    EventDescription TEXT NULL,
                    ProgramName TEXT NULL,
                    ProcessId INTEGER NULL,
                    FilePath TEXT NULL,
                    WindowTitle TEXT NULL,
                    UserName TEXT NULL,
                    MachineName TEXT NULL,
                    AppVersion TEXT NULL,
                    Note TEXT NULL
                );
                """);

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_UserEvents_CreatedAt ON UserEvents (CreatedAt);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_UserEvents_EventType ON UserEvents (EventType);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ProgramEvents_CreatedAt ON ProgramEvents (CreatedAt);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ProgramEvents_EventType ON ProgramEvents (EventType);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ProgramEvents_ProgramName ON ProgramEvents (ProgramName);"
            );
        }
    }

    public void InsertActivityEvent(ActivityEventRecord record)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            using var command = connection.CreateCommand();

            command.CommandText =
                """
                INSERT INTO ActivityEvents
                (
                    CreatedAt,
                    ActivityType,
                    Item,
                    FullPath,
                    FolderPath,
                    FileName,
                    Extension,
                    DateOpened,
                    TimeOpened,
                    SizeAtOpening,
                    FirstWriteDate,
                    FirstWriteTime,
                    LastWriteDate,
                    LastWriteTime,
                    WriteCount,
                    SizeAtLastWrite,
                    DateClosed,
                    TimeClosed,
                    SizeAtClosing,
                    InferredAction,
                    ProcessName,
                    ProcessId,
                    Note
                )
                VALUES
                (
                    $CreatedAt,
                    $ActivityType,
                    $Item,
                    $FullPath,
                    $FolderPath,
                    $FileName,
                    $Extension,
                    $DateOpened,
                    $TimeOpened,
                    $SizeAtOpening,
                    $FirstWriteDate,
                    $FirstWriteTime,
                    $LastWriteDate,
                    $LastWriteTime,
                    $WriteCount,
                    $SizeAtLastWrite,
                    $DateClosed,
                    $TimeClosed,
                    $SizeAtClosing,
                    $InferredAction,
                    $ProcessName,
                    $ProcessId,
                    $Note
                );
                """;

            AddText(command, "$CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            AddText(command, "$ActivityType", record.ActivityType);
            AddText(command, "$Item", record.FullPath);
            AddText(command, "$FullPath", record.FullPath);
            AddText(command, "$FolderPath", record.FolderPath);
            AddText(command, "$FileName", record.FileName);
            AddText(command, "$Extension", record.Extension);
            AddText(command, "$DateOpened", FormatDate(record.OpenedTime));
            AddText(command, "$TimeOpened", FormatTime(record.OpenedTime));
            AddInteger(command, "$SizeAtOpening", record.SizeAtOpening);
            AddText(command, "$FirstWriteDate", FormatDate(record.FirstWriteTime));
            AddText(command, "$FirstWriteTime", FormatTime(record.FirstWriteTime));
            AddText(command, "$LastWriteDate", FormatDate(record.LastWriteTime));
            AddText(command, "$LastWriteTime", FormatTime(record.LastWriteTime));
            AddInteger(command, "$WriteCount", record.WriteCount > 0 ? record.WriteCount : null);
            AddInteger(command, "$SizeAtLastWrite", record.SizeAtLastWrite);
            AddText(command, "$DateClosed", FormatDate(record.ClosedTime));
            AddText(command, "$TimeClosed", FormatTime(record.ClosedTime));
            AddInteger(command, "$SizeAtClosing", record.SizeAtClosing);
            AddText(command, "$InferredAction", record.InferredAction);
            AddText(command, "$ProcessName", record.ProcessName);
            AddInteger(command, "$ProcessId", record.ProcessId);
            AddText(command, "$Note", record.Note);

            command.ExecuteNonQuery();
        }
    }

    public void InsertUserEvent(UserEventRecord record)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            using var command = connection.CreateCommand();

            command.CommandText =
                """
                INSERT INTO UserEvents
                (
                    CreatedAt,
                    EventDate,
                    EventTime,
                    EventType,
                    EventDescription,
                    UserName,
                    MachineName,
                    ProcessName,
                    ProcessId,
                    AppVersion,
                    Note
                )
                VALUES
                (
                    $CreatedAt,
                    $EventDate,
                    $EventTime,
                    $EventType,
                    $EventDescription,
                    $UserName,
                    $MachineName,
                    $ProcessName,
                    $ProcessId,
                    $AppVersion,
                    $Note
                );
                """;

            var eventTime = record.EventTime == default ? DateTime.Now : record.EventTime;

            AddText(command, "$CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            AddText(command, "$EventDate", eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AddText(command, "$EventTime", eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            AddText(command, "$EventType", record.EventType);
            AddText(command, "$EventDescription", record.EventDescription);
            AddText(command, "$UserName", record.UserName);
            AddText(command, "$MachineName", record.MachineName);
            AddText(command, "$ProcessName", record.ProcessName);
            AddInteger(command, "$ProcessId", record.ProcessId);
            AddText(command, "$AppVersion", record.AppVersion);
            AddText(command, "$Note", record.Note);

            command.ExecuteNonQuery();
        }
    }

    public void InsertProgramEvent(ProgramEventRecord record)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            using var command = connection.CreateCommand();

            command.CommandText =
                """
                INSERT INTO ProgramEvents
                (
                    CreatedAt,
                    EventDate,
                    EventTime,
                    EventType,
                    EventDescription,
                    ProgramName,
                    ProcessId,
                    FilePath,
                    WindowTitle,
                    UserName,
                    MachineName,
                    AppVersion,
                    Note
                )
                VALUES
                (
                    $CreatedAt,
                    $EventDate,
                    $EventTime,
                    $EventType,
                    $EventDescription,
                    $ProgramName,
                    $ProcessId,
                    $FilePath,
                    $WindowTitle,
                    $UserName,
                    $MachineName,
                    $AppVersion,
                    $Note
                );
                """;

            var eventTime = record.EventTime == default ? DateTime.Now : record.EventTime;

            AddText(command, "$CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            AddText(command, "$EventDate", eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AddText(command, "$EventTime", eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            AddText(command, "$EventType", record.EventType);
            AddText(command, "$EventDescription", record.EventDescription);
            AddText(command, "$ProgramName", record.ProgramName);
            AddInteger(command, "$ProcessId", record.ProcessId);
            AddText(command, "$FilePath", record.FilePath);
            AddText(command, "$WindowTitle", record.WindowTitle);
            AddText(command, "$UserName", record.UserName);
            AddText(command, "$MachineName", record.MachineName);
            AddText(command, "$AppVersion", record.AppVersion);
            AddText(command, "$Note", record.Note);

            command.ExecuteNonQuery();
        }
    }

    public MaintenanceDatabaseOverview GetMaintenanceOverview()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            var databaseBytes = GetFileLength(DatabaseFilePath);
            var walBytes = GetFileLength(DatabaseFilePath + "-wal");
            var shmBytes = GetFileLength(DatabaseFilePath + "-shm");

            return new MaintenanceDatabaseOverview
            {
                DatabaseBytes = databaseBytes,
                WalBytes = walBytes,
                ShmBytes = shmBytes,
                ActivityEventCount = CountTableRows(connection, "ActivityEvents"),
                UserEventCount = CountTableRows(connection, "UserEvents"),
                ProgramEventCount = CountTableRows(connection, "ProgramEvents")
            };
        }
    }

    public List<MaintenanceStatisticRow> GetTopMaintenanceStatistics(string viewName)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            var normalizedViewName = viewName.Trim();

            if (normalizedViewName.Equals("Top 100 folders", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTopStatistics(
                    connection,
                    """
                    SELECT
                        COALESCE(NULLIF(FolderPath, ''), '(blank)') AS Value,
                        COALESCE(NULLIF(Extension, ''), '(mixed)') AS Extra,
                        COUNT(*) AS EventCount,
                        MIN(CreatedAt) AS FirstSeen,
                        MAX(CreatedAt) AS LastSeen
                    FROM ActivityEvents
                    GROUP BY COALESCE(NULLIF(FolderPath, ''), '(blank)'), COALESCE(NULLIF(Extension, ''), '(mixed)')
                    ORDER BY EventCount DESC, Value ASC
                    LIMIT 100;
                    """);
            }

            if (normalizedViewName.Equals("Top 100 file processes", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTopStatistics(
                    connection,
                    """
                    SELECT
                        COALESCE(NULLIF(ProcessName, ''), '(blank)') AS Value,
                        COALESCE(NULLIF(Extension, ''), '(mixed)') AS Extra,
                        COUNT(*) AS EventCount,
                        MIN(CreatedAt) AS FirstSeen,
                        MAX(CreatedAt) AS LastSeen
                    FROM ActivityEvents
                    GROUP BY COALESCE(NULLIF(ProcessName, ''), '(blank)'), COALESCE(NULLIF(Extension, ''), '(mixed)')
                    ORDER BY EventCount DESC, Value ASC
                    LIMIT 100;
                    """);
            }

            if (normalizedViewName.Equals("Top 100 extensions", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTopStatistics(
                    connection,
                    """
                    SELECT
                        COALESCE(NULLIF(Extension, ''), '(blank)') AS Value,
                        COALESCE(NULLIF(ProcessName, ''), '(mixed)') AS Extra,
                        COUNT(*) AS EventCount,
                        MIN(CreatedAt) AS FirstSeen,
                        MAX(CreatedAt) AS LastSeen
                    FROM ActivityEvents
                    GROUP BY COALESCE(NULLIF(Extension, ''), '(blank)'), COALESCE(NULLIF(ProcessName, ''), '(mixed)')
                    ORDER BY EventCount DESC, Value ASC
                    LIMIT 100;
                    """);
            }

            if (normalizedViewName.Equals("Top 100 program events", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTopStatistics(
                    connection,
                    """
                    SELECT
                        COALESCE(NULLIF(ProgramName, ''), '(blank)') AS Value,
                        COALESCE(NULLIF(EventType, ''), '(mixed)') AS Extra,
                        COUNT(*) AS EventCount,
                        MIN(CreatedAt) AS FirstSeen,
                        MAX(CreatedAt) AS LastSeen
                    FROM ProgramEvents
                    GROUP BY COALESCE(NULLIF(ProgramName, ''), '(blank)'), COALESCE(NULLIF(EventType, ''), '(mixed)')
                    ORDER BY EventCount DESC, Value ASC
                    LIMIT 100;
                    """);
            }

            return ReadTopStatistics(
                connection,
                """
                SELECT
                    COALESCE(NULLIF(FullPath, ''), NULLIF(Item, ''), '(blank)') AS Value,
                    COALESCE(NULLIF(ProcessName, ''), '(blank)') AS Extra,
                    COUNT(*) AS EventCount,
                    MIN(CreatedAt) AS FirstSeen,
                    MAX(CreatedAt) AS LastSeen
                FROM ActivityEvents
                GROUP BY COALESCE(NULLIF(FullPath, ''), NULLIF(Item, ''), '(blank)'), COALESCE(NULLIF(ProcessName, ''), '(blank)')
                ORDER BY EventCount DESC, Value ASC
                LIMIT 100;
                """);
        }
    }

    public long ClearTableRecords(string tableName)
    {
        if (!IsKnownActivityTable(tableName))
            throw new InvalidOperationException("Unknown DeskPulse table: " + tableName);

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            var before = CountTableRows(connection, tableName);
            ExecuteNonQuery(connection, "DELETE FROM " + tableName + ";");
            ExecuteNonQuery(connection, "VACUUM;");
            return before;
        }
    }

    public long ClearAllRecords()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            var total = CountTableRows(connection, "ActivityEvents") +
                        CountTableRows(connection, "UserEvents") +
                        CountTableRows(connection, "ProgramEvents");

            ExecuteNonQuery(connection, "DELETE FROM ActivityEvents;");
            ExecuteNonQuery(connection, "DELETE FROM UserEvents;");
            ExecuteNonQuery(connection, "DELETE FROM ProgramEvents;");
            ExecuteNonQuery(connection, "VACUUM;");
            return total;
        }
    }

    public MaintenanceExclusionCleanupResult RemoveRecordsMatchingExclusions(
        IReadOnlyList<string> excludedFolders,
        IReadOnlyCollection<string> excludedProcesses,
        IReadOnlyList<string> excludedFiles,
        IProgress<ExportProgressInfo>? progress = null)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            progress?.Report(new ExportProgressInfo(1, "1%   Determining matching records"));

            var activityIdsToDelete = FindActivityEventIdsMatchingExclusions(connection, excludedFolders, excludedProcesses, excludedFiles);
            var programIdsToDelete = FindProgramEventIdsMatchingExclusions(connection, excludedFolders, excludedProcesses);
            var totalRecordsToDelete = activityIdsToDelete.Count + programIdsToDelete.Count;
            var recordsDeleted = 0;

            progress?.Report(new ExportProgressInfo(1, "1%   Matching records determined: " + totalRecordsToDelete.ToString("N0", CultureInfo.InvariantCulture)));

            if (totalRecordsToDelete == 0)
            {
                progress?.Report(new ExportProgressInfo(100, "100% No matching records found"));
                return new MaintenanceExclusionCleanupResult
                {
                    ActivityRecordsDeleted = 0,
                    ProgramRecordsDeleted = 0
                };
            }

            void ReportDeleteProgress(int deletedInStep, string tableDescription)
            {
                recordsDeleted += Math.Max(0, deletedInStep);
                var deletionProgress = (double)recordsDeleted / Math.Max(1, totalRecordsToDelete);
                var percent = 1 + (int)Math.Round(deletionProgress * 99D, MidpointRounding.AwayFromZero);
                percent = Math.Max(1, Math.Min(100, percent));

                progress?.Report(new ExportProgressInfo(
                    percent,
                    percent.ToString(CultureInfo.InvariantCulture) + "%   Deleting " + tableDescription + " (" + recordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + " of " + totalRecordsToDelete.ToString("N0", CultureInfo.InvariantCulture) + " records)"));
            }

            DeleteRowsByIds(connection, "ActivityEvents", activityIdsToDelete, deleted => ReportDeleteProgress(deleted, "file activity records"));
            DeleteRowsByIds(connection, "ProgramEvents", programIdsToDelete, deleted => ReportDeleteProgress(deleted, "program activity records"));

            if (totalRecordsToDelete > 0)
            {
                progress?.Report(new ExportProgressInfo(100, "100% Deletion complete; compacting database"));
                ExecuteNonQuery(connection, "VACUUM;");
            }

            progress?.Report(new ExportProgressInfo(100, "100% Excluded past records removed"));

            return new MaintenanceExclusionCleanupResult
            {
                ActivityRecordsDeleted = activityIdsToDelete.Count,
                ProgramRecordsDeleted = programIdsToDelete.Count
            };
        }
    }

    private static List<long> FindActivityEventIdsMatchingExclusions(
        SqliteConnection connection,
        IReadOnlyList<string> excludedFolders,
        IReadOnlyCollection<string> excludedProcesses,
        IReadOnlyList<string> excludedFiles)
    {
        var result = new List<long>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                COALESCE(FullPath, ''),
                COALESCE(FolderPath, ''),
                COALESCE(ProcessName, '')
            FROM ActivityEvents;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var fullPath = ReadText(reader, 1);
            var folderPath = ReadText(reader, 2);
            var processName = ReadText(reader, 3);

            var matchesFolder =
                PathExclusions.IsInExcludedFolder(fullPath, excludedFolders) ||
                PathExclusions.IsInExcludedFolder(folderPath, excludedFolders);

            var matchesFile = FileExclusions.IsExactExcludedFile(fullPath, excludedFiles);
            var matchesProcess = ProcessExclusions.IsExcludedProcess(processName, excludedProcesses);

            if (matchesFolder || matchesFile || matchesProcess)
                result.Add(id);
        }

        return result;
    }

    private static List<long> FindProgramEventIdsMatchingExclusions(
        SqliteConnection connection,
        IReadOnlyList<string> excludedFolders,
        IReadOnlyCollection<string> excludedProcesses)
    {
        var result = new List<long>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                COALESCE(ProgramName, ''),
                COALESCE(FilePath, '')
            FROM ProgramEvents;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var programName = ReadText(reader, 1);
            var filePath = ReadText(reader, 2);

            var matchesFolder = PathExclusions.IsInExcludedFolder(filePath, excludedFolders);
            var matchesProcess = ProcessExclusions.IsExcludedProcess(programName, excludedProcesses);

            if (matchesFolder || matchesProcess)
                result.Add(id);
        }

        return result;
    }

    private static void DeleteRowsByIds(SqliteConnection connection, string tableName, IReadOnlyList<long> ids, Action<int>? progressCallback = null)
    {
        if (ids.Count == 0)
            return;

        foreach (var chunk in ids.Chunk(500))
        {
            using var command = connection.CreateCommand();

            var parameterNames = new List<string>();

            for (var i = 0; i < chunk.Length; i++)
            {
                var parameterName = "$id" + i.ToString(CultureInfo.InvariantCulture);
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, chunk[i]);
            }

            command.CommandText = "DELETE FROM " + tableName + " WHERE Id IN (" + string.Join(",", parameterNames) + ");";
            command.ExecuteNonQuery();
            progressCallback?.Invoke(chunk.Length);
        }
    }


    public void ExportToExcel(string excelFilePath, IReadOnlyList<ExportSheetOption> exportSheets, DateTime startDate, DateTime endDate, IProgress<ExportProgressInfo>? progress = null)
    {
        lock (_dbLock)
        {
            var folder = Path.GetDirectoryName(excelFilePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            progress?.Report(new ExportProgressInfo(1, "1%   Reading activity records"));

            var rangeStart = startDate.Date;
            var rangeEnd = endDate.Date;

            if (rangeEnd < rangeStart)
            {
                (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
            }

            var rows = ReadAllRows()
                .Where(row => IsActivityRowInsideDateRange(row, rangeStart, rangeEnd))
                .ToList();

            var userRows = ReadAllUserRows()
                .Where(row => IsUserRowInsideDateRange(row, rangeStart, rangeEnd))
                .ToList();

            var programRows = ReadAllProgramRows()
                .Where(row => IsProgramRowInsideDateRange(row, rangeStart, rangeEnd))
                .ToList();

            var selectedSheets = ExportSheetOption.NormalizeList(exportSheets);

            progress?.Report(new ExportProgressInfo(3, "3%   Counting rows to export"));

            var totalRowsToWrite = CountRowsToWrite(selectedSheets, rows, userRows, programRows);
            var progressTracker = new ExcelExportProgressTracker(progress, totalRowsToWrite);

            using var workbook = new XLWorkbook();

            foreach (var sheetOption in selectedSheets)
            {
                if (sheetOption.Id == ExportSheetOption.FileActivityId)
                    AddFileActivityWorksheet(workbook, rows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.DailySummaryId)
                    AddDailySummaryWorksheet(workbook, rows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.ExtensionSummaryId)
                    AddExtensionSummaryWorksheet(workbook, rows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.ProcessSummaryId)
                    AddProcessSummaryWorksheet(workbook, rows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.ErrorsId)
                    AddErrorsWorksheet(workbook, rows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.UserId)
                    AddUserWorksheet(workbook, userRows, sheetOption, progressTracker);
                else if (sheetOption.Id == ExportSheetOption.ProgramActivityId)
                    AddProgramActivityWorksheet(workbook, programRows, sheetOption, progressTracker);
            }

            if (!workbook.Worksheets.Any())
                AddFileActivityWorksheet(workbook, rows, ExportSheetOption.GetDefaultOptions()[0], progressTracker);

            progress?.Report(new ExportProgressInfo(94, "94%  Saving workbook"));

            var exportFolder = Path.GetDirectoryName(excelFilePath) ?? AppContext.BaseDirectory;
            var exportFileNameWithoutExtension = Path.GetFileNameWithoutExtension(excelFilePath);

            var tempFilePath = Path.Combine(
                exportFolder,
                $"{exportFileNameWithoutExtension}-{Guid.NewGuid():N}.xlsx"
            );

            workbook.SaveAs(tempFilePath);

            progress?.Report(new ExportProgressInfo(97, "97%  Replacing previous export file"));

            if (File.Exists(excelFilePath))
                File.Delete(excelFilePath);

            File.Move(tempFilePath, excelFilePath);
        }
    }

    private static int CountRowsToWrite(IReadOnlyList<ExportSheetOption> selectedSheets, List<ExcelExportRow> rows, List<UserExportRow> userRows, List<ProgramExportRow> programRows)
    {
        var totalRows = 0;

        foreach (var sheetOption in selectedSheets)
        {
            if (sheetOption.Id == ExportSheetOption.FileActivityId)
                totalRows += rows.Count;
            else if (sheetOption.Id == ExportSheetOption.DailySummaryId)
                totalRows += rows.GroupBy(row => GetRowDate(row)).Count();
            else if (sheetOption.Id == ExportSheetOption.ExtensionSummaryId)
                totalRows += rows.Where(row => !string.IsNullOrWhiteSpace(row.Extension)).GroupBy(row => row.Extension, StringComparer.OrdinalIgnoreCase).Count();
            else if (sheetOption.Id == ExportSheetOption.ProcessSummaryId)
                totalRows += rows.GroupBy(row => new { row.ProcessName, ProcessId = row.ProcessId ?? 0 }).Count();
            else if (sheetOption.Id == ExportSheetOption.ErrorsId)
                totalRows += rows.Count(row => string.Equals(row.ActivityType, "Error", StringComparison.OrdinalIgnoreCase));
            else if (sheetOption.Id == ExportSheetOption.UserId)
                totalRows += userRows.Count;
            else if (sheetOption.Id == ExportSheetOption.ProgramActivityId)
                totalRows += programRows.Count;
        }

        if (totalRows == 0)
            return 1;

        return totalRows;
    }

    private static void AddFileActivityWorksheet(XLWorkbook workbook, List<ExcelExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();

        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var rowNumber = 2;

        foreach (var row in rows)
        {
            for (var col = 0; col < fields.Count; col++)
                WriteFileActivityCell(sheet.Cell(rowNumber, col + 1), row, fields[col].Id);

            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddDailySummaryWorksheet(XLWorkbook workbook, List<ExcelExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var summaryRows = rows
            .GroupBy(row => GetRowDate(row))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rowNumber = 2;

        foreach (var group in summaryRows)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["date"] = group.Key,
                ["events"] = group.LongCount(),
                ["open-events"] = group.LongCount(row => !string.IsNullOrWhiteSpace(row.DateOpened)),
                ["write-events"] = group.LongCount(row => row.WriteCount.GetValueOrDefault() > 0 || ContainsText(row.InferredAction, "write") || ContainsText(row.InferredAction, "saved")),
                ["close-events"] = group.LongCount(row => !string.IsNullOrWhiteSpace(row.DateClosed)),
                ["error-events"] = group.LongCount(row => string.Equals(row.ActivityType, "Error", StringComparison.OrdinalIgnoreCase)),
                ["distinct-files"] = group.Select(row => row.FullPath).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).LongCount(),
                ["distinct-processes"] = group.Select(row => row.ProcessName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).LongCount()
            };

            WriteDictionaryRow(sheet, rowNumber, fields, values);
            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddExtensionSummaryWorksheet(XLWorkbook workbook, List<ExcelExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var summaryRows = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Extension))
            .GroupBy(row => row.Extension, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.LongCount())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rowNumber = 2;

        foreach (var group in summaryRows)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["extension"] = group.Key,
                ["events"] = group.LongCount(),
                ["distinct-files"] = group.Select(row => row.FullPath).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).LongCount(),
                ["write-events"] = group.LongCount(row => row.WriteCount.GetValueOrDefault() > 0 || ContainsText(row.InferredAction, "write") || ContainsText(row.InferredAction, "saved")),
                ["last-event"] = group.Max(row => row.CreatedAt)
            };

            WriteDictionaryRow(sheet, rowNumber, fields, values);
            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddProcessSummaryWorksheet(XLWorkbook workbook, List<ExcelExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var summaryRows = rows
            .GroupBy(row => new { row.ProcessName, ProcessId = row.ProcessId ?? 0 })
            .OrderByDescending(group => group.LongCount())
            .ThenBy(group => group.Key.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rowNumber = 2;

        foreach (var group in summaryRows)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["process"] = string.IsNullOrWhiteSpace(group.Key.ProcessName) ? "UnknownProcess" : group.Key.ProcessName,
                ["process-id"] = group.Key.ProcessId == 0 ? null : group.Key.ProcessId,
                ["events"] = group.LongCount(),
                ["distinct-files"] = group.Select(row => row.FullPath).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).LongCount(),
                ["write-events"] = group.LongCount(row => row.WriteCount.GetValueOrDefault() > 0 || ContainsText(row.InferredAction, "write") || ContainsText(row.InferredAction, "saved")),
                ["last-event"] = group.Max(row => row.CreatedAt)
            };

            WriteDictionaryRow(sheet, rowNumber, fields, values);
            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddErrorsWorksheet(XLWorkbook workbook, List<ExcelExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var errorRows = rows
            .Where(row => string.Equals(row.ActivityType, "Error", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Id)
            .ToList();

        var rowNumber = 2;

        foreach (var row in errorRows)
        {
            for (var col = 0; col < fields.Count; col++)
                WriteFileActivityCell(sheet.Cell(rowNumber, col + 1), row, fields[col].Id);

            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddUserWorksheet(XLWorkbook workbook, List<UserExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var rowNumber = 2;

        foreach (var row in rows)
        {
            for (var col = 0; col < fields.Count; col++)
                WriteUserCell(sheet.Cell(rowNumber, col + 1), row, fields[col].Id);

            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void AddProgramActivityWorksheet(XLWorkbook workbook, List<ProgramExportRow> rows, ExportSheetOption sheetOption, ExcelExportProgressTracker progressTracker)
    {
        var sheet = workbook.Worksheets.Add(sheetOption.WorksheetName);
        var fields = sheetOption.GetSelectedFields();
        WriteHeaders(sheet, fields.Select(field => field.HeaderName).ToArray());

        var rowNumber = 2;

        foreach (var row in rows)
        {
            for (var col = 0; col < fields.Count; col++)
                WriteProgramCell(sheet.Cell(rowNumber, col + 1), row, fields[col].Id);

            rowNumber++;
            progressTracker.ReportRowsWritten(1, sheetOption.WorksheetName);
        }

        progressTracker.ReportWorksheetComplete(sheetOption.WorksheetName);
        FinishWorksheet(sheet, rowNumber);
    }

    private static void WriteDictionaryRow(IXLWorksheet sheet, int rowNumber, IReadOnlyList<ExportFieldOption> fields, IReadOnlyDictionary<string, object?> values)
    {
        for (var col = 0; col < fields.Count; col++)
        {
            values.TryGetValue(fields[col].Id, out var value);
            SetAnyCellValue(sheet.Cell(rowNumber, col + 1), value);
        }
    }

    private static void WriteFileActivityCell(IXLCell cell, ExcelExportRow row, string fieldId)
    {
        switch (fieldId)
        {
            case "id":
                SetCellValue(cell, row.Id);
                break;
            case "created-at":
                cell.Value = row.CreatedAt;
                break;
            case "activity-type":
                cell.Value = row.ActivityType;
                break;
            case "full-path":
            case "item":
                cell.Value = row.FullPath;
                break;
            case "folder-path":
                cell.Value = row.FolderPath;
                break;
            case "file-name":
                cell.Value = row.FileName;
                break;
            case "extension":
                cell.Value = row.Extension;
                break;
            case "date-opened":
                cell.Value = row.DateOpened;
                break;
            case "time-opened":
                cell.Value = row.TimeOpened;
                break;
            case "size-at-opening":
                SetCellValue(cell, row.SizeAtOpening);
                break;
            case "first-write-date":
                cell.Value = row.FirstWriteDate;
                break;
            case "first-write-time":
                cell.Value = row.FirstWriteTime;
                break;
            case "last-write-date":
                cell.Value = row.LastWriteDate;
                break;
            case "last-write-time":
                cell.Value = row.LastWriteTime;
                break;
            case "write-count":
                SetCellValue(cell, row.WriteCount);
                break;
            case "size-at-last-write":
                SetCellValue(cell, row.SizeAtLastWrite);
                break;
            case "date-closed":
                cell.Value = row.DateClosed;
                break;
            case "time-closed":
                cell.Value = row.TimeClosed;
                break;
            case "size-at-closing":
                SetCellValue(cell, row.SizeAtClosing);
                break;
            case "inferred-action":
                cell.Value = row.InferredAction;
                break;
            case "process-name":
            case "process":
                cell.Value = row.ProcessName;
                break;
            case "process-id":
                SetCellValue(cell, row.ProcessId);
                break;
            case "note":
                cell.Value = row.Note;
                break;
            default:
                cell.Value = "";
                break;
        }
    }

    private static void WriteUserCell(IXLCell cell, UserExportRow row, string fieldId)
    {
        switch (fieldId)
        {
            case "id":
                SetCellValue(cell, row.Id);
                break;
            case "created-at":
                cell.Value = row.CreatedAt;
                break;
            case "event-date":
                cell.Value = row.EventDate;
                break;
            case "event-time":
                cell.Value = row.EventTime;
                break;
            case "event-type":
                cell.Value = row.EventType;
                break;
            case "event-description":
                cell.Value = row.EventDescription;
                break;
            case "user-name":
                cell.Value = row.UserName;
                break;
            case "machine-name":
                cell.Value = row.MachineName;
                break;
            case "process-name":
            case "process":
                cell.Value = row.ProcessName;
                break;
            case "process-id":
                SetCellValue(cell, row.ProcessId);
                break;
            case "app-version":
                cell.Value = row.AppVersion;
                break;
            case "note":
                cell.Value = row.Note;
                break;
            default:
                cell.Value = "";
                break;
        }
    }

    private static void WriteProgramCell(IXLCell cell, ProgramExportRow row, string fieldId)
    {
        switch (fieldId)
        {
            case "id":
                SetCellValue(cell, row.Id);
                break;
            case "created-at":
                cell.Value = row.CreatedAt;
                break;
            case "event-date":
                cell.Value = row.EventDate;
                break;
            case "event-time":
                cell.Value = row.EventTime;
                break;
            case "event-type":
                cell.Value = row.EventType;
                break;
            case "event-description":
                cell.Value = row.EventDescription;
                break;
            case "program-name":
            case "process-name":
            case "process":
                cell.Value = row.ProgramName;
                break;
            case "process-id":
                SetCellValue(cell, row.ProcessId);
                break;
            case "file-path":
                cell.Value = row.FilePath;
                break;
            case "window-title":
                cell.Value = row.WindowTitle;
                break;
            case "user-name":
                cell.Value = row.UserName;
                break;
            case "machine-name":
                cell.Value = row.MachineName;
                break;
            case "app-version":
                cell.Value = row.AppVersion;
                break;
            case "note":
                cell.Value = row.Note;
                break;
            default:
                cell.Value = "";
                break;
        }
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
        }
    }

    private static void FinishWorksheet(IXLWorksheet sheet, int rowNumber)
    {
        sheet.SheetView.FreezeRows(1);

        if (sheet.RangeUsed() != null)
            sheet.RangeUsed()!.SetAutoFilter();

        sheet.Columns().AdjustToContents(1, Math.Min(rowNumber, 250));
    }

    private static string GetRowDate(ExcelExportRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.DateOpened))
            return row.DateOpened;

        if (!string.IsNullOrWhiteSpace(row.FirstWriteDate))
            return row.FirstWriteDate;

        if (!string.IsNullOrWhiteSpace(row.DateClosed))
            return row.DateClosed;

        if (!string.IsNullOrWhiteSpace(row.CreatedAt) && row.CreatedAt.Length >= 10)
            return row.CreatedAt.Substring(0, 10);

        return "Unknown";
    }
    private static bool IsActivityRowInsideDateRange(ExcelExportRow row, DateTime rangeStart, DateTime rangeEnd)
    {
        return IsDateTextInsideRange(GetRowDate(row), rangeStart, rangeEnd);
    }

    private static bool IsUserRowInsideDateRange(UserExportRow row, DateTime rangeStart, DateTime rangeEnd)
    {
        var rowDate = !string.IsNullOrWhiteSpace(row.EventDate)
            ? row.EventDate
            : row.CreatedAt;

        return IsDateTextInsideRange(rowDate, rangeStart, rangeEnd);
    }

    private static bool IsProgramRowInsideDateRange(ProgramExportRow row, DateTime rangeStart, DateTime rangeEnd)
    {
        var rowDate = !string.IsNullOrWhiteSpace(row.EventDate)
            ? row.EventDate
            : row.CreatedAt;

        return IsDateTextInsideRange(rowDate, rangeStart, rangeEnd);
    }

    private static bool IsDateTextInsideRange(string dateText, DateTime rangeStart, DateTime rangeEnd)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return false;

        var candidate = dateText.Trim();

        if (candidate.Length >= 10)
            candidate = candidate.Substring(0, 10);

        if (!DateTime.TryParseExact(
                candidate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }

        var day = date.Date;
        return day >= rangeStart && day <= rangeEnd;
    }


    private static bool ContainsText(string value, string searchText)
    {
        return value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetCellValue(IXLCell cell, long? value)
    {
        if (value == null)
            cell.Value = "";
        else
            cell.Value = value.Value;
    }

    private static void SetAnyCellValue(IXLCell cell, object? value)
    {
        if (value == null)
        {
            cell.Value = "";
            return;
        }

        if (value is long longValue)
        {
            SetCellValue(cell, longValue);
            return;
        }

        if (value is int intValue)
        {
            SetCellValue(cell, intValue);
            return;
        }

        cell.Value = value.ToString() ?? "";
    }

    private static List<MaintenanceStatisticRow> ReadTopStatistics(SqliteConnection connection, string sql)
    {
        var result = new List<MaintenanceStatisticRow>();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var rank = 1;

        while (reader.Read())
        {
            result.Add(new MaintenanceStatisticRow
            {
                Rank = rank++,
                Value = ReadText(reader, 0),
                Extra = ReadText(reader, 1),
                Count = ReadLong(reader, 2),
                FirstSeen = ReadText(reader, 3),
                LastSeen = ReadText(reader, 4)
            });
        }

        return result;
    }

    private static long CountTableRows(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value
            ? 0
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static bool IsKnownActivityTable(string tableName)
    {
        return tableName.Equals("ActivityEvents", StringComparison.OrdinalIgnoreCase) ||
               tableName.Equals("UserEvents", StringComparison.OrdinalIgnoreCase) ||
               tableName.Equals("ProgramEvents", StringComparison.OrdinalIgnoreCase);
    }

    private static long GetFileLength(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private List<ExcelExportRow> ReadAllRows()
    {
        var result = new List<ExcelExportRow>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                CreatedAt,
                ActivityType,
                Item,
                FullPath,
                FolderPath,
                FileName,
                Extension,
                DateOpened,
                TimeOpened,
                SizeAtOpening,
                FirstWriteDate,
                FirstWriteTime,
                LastWriteDate,
                LastWriteTime,
                WriteCount,
                SizeAtLastWrite,
                DateClosed,
                TimeClosed,
                SizeAtClosing,
                InferredAction,
                ProcessName,
                ProcessId,
                Note
            FROM ActivityEvents
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var legacyItem = ReadText(reader, 3);
            var fullPath = ReadText(reader, 4);

            if (string.IsNullOrWhiteSpace(fullPath))
                fullPath = legacyItem;

            fullPath = PathUtilities.NormalizeEtwPath(fullPath);

            var folderPath = ReadText(reader, 5);
            var fileName = ReadText(reader, 6);
            var extension = ReadText(reader, 7);

            if (string.IsNullOrWhiteSpace(folderPath))
                folderPath = PathUtilities.GetFolderPath(fullPath);

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = PathUtilities.GetFileNameOnly(fullPath);

            if (string.IsNullOrWhiteSpace(extension))
                extension = PathUtilities.GetExtension(fullPath);

            result.Add(new ExcelExportRow
            {
                Id = ReadLong(reader, 0),
                CreatedAt = ReadText(reader, 1),
                ActivityType = ReadText(reader, 2),
                FullPath = fullPath,
                FolderPath = folderPath,
                FileName = fileName,
                Extension = extension,
                DateOpened = ReadText(reader, 8),
                TimeOpened = ReadText(reader, 9),
                SizeAtOpening = ReadNullableLong(reader, 10),
                FirstWriteDate = ReadText(reader, 11),
                FirstWriteTime = ReadText(reader, 12),
                LastWriteDate = ReadText(reader, 13),
                LastWriteTime = ReadText(reader, 14),
                WriteCount = ReadNullableLong(reader, 15),
                SizeAtLastWrite = ReadNullableLong(reader, 16),
                DateClosed = ReadText(reader, 17),
                TimeClosed = ReadText(reader, 18),
                SizeAtClosing = ReadNullableLong(reader, 19),
                InferredAction = ReadText(reader, 20),
                ProcessName = ReadText(reader, 21),
                ProcessId = ReadNullableLong(reader, 22),
                Note = ReadText(reader, 23)
            });
        }

        return result;
    }

    private List<UserExportRow> ReadAllUserRows()
    {
        var result = new List<UserExportRow>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                CreatedAt,
                EventDate,
                EventTime,
                EventType,
                EventDescription,
                UserName,
                MachineName,
                ProcessName,
                ProcessId,
                AppVersion,
                Note
            FROM UserEvents
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(new UserExportRow
            {
                Id = ReadLong(reader, 0),
                CreatedAt = ReadText(reader, 1),
                EventDate = ReadText(reader, 2),
                EventTime = ReadText(reader, 3),
                EventType = ReadText(reader, 4),
                EventDescription = ReadText(reader, 5),
                UserName = ReadText(reader, 6),
                MachineName = ReadText(reader, 7),
                ProcessName = ReadText(reader, 8),
                ProcessId = ReadNullableLong(reader, 9),
                AppVersion = ReadText(reader, 10),
                Note = ReadText(reader, 11)
            });
        }

        return result;
    }

    private List<ProgramExportRow> ReadAllProgramRows()
    {
        var result = new List<ProgramExportRow>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                CreatedAt,
                EventDate,
                EventTime,
                EventType,
                EventDescription,
                ProgramName,
                ProcessId,
                FilePath,
                WindowTitle,
                UserName,
                MachineName,
                AppVersion,
                Note
            FROM ProgramEvents
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(new ProgramExportRow
            {
                Id = ReadLong(reader, 0),
                CreatedAt = ReadText(reader, 1),
                EventDate = ReadText(reader, 2),
                EventTime = ReadText(reader, 3),
                EventType = ReadText(reader, 4),
                EventDescription = ReadText(reader, 5),
                ProgramName = ReadText(reader, 6),
                ProcessId = ReadNullableLong(reader, 7),
                FilePath = ReadText(reader, 8),
                WindowTitle = ReadText(reader, 9),
                UserName = ReadText(reader, 10),
                MachineName = ReadText(reader, 11),
                AppVersion = ReadText(reader, 12),
                Note = ReadText(reader, 13)
            });
        }

        return result;
    }

    private static void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(connection, tableName, columnName))
            return;

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var existingColumnName = reader.GetString(1);

            if (existingColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddText(SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
    }

    private static void AddInteger(SqliteCommand command, string name, long? value)
    {
        command.Parameters.AddWithValue(name, value == null ? DBNull.Value : value.Value);
    }

    private static string FormatDate(DateTime? value)
    {
        return value == null
            ? ""
            : value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(DateTime? value)
    {
        return value == null
            ? ""
            : value.Value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static long ReadLong(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? 0
            : reader.GetInt64(ordinal);
    }

    private static long? ReadNullableLong(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    private static string ReadText(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? ""
            : reader.GetString(ordinal);
    }

    public void Dispose()
    {
        // Connections are short-lived and disposed per operation.
    }
}

public sealed class MaintenanceDatabaseOverview
{
    public long DatabaseBytes { get; set; }
    public long WalBytes { get; set; }
    public long ShmBytes { get; set; }
    public long ActivityEventCount { get; set; }
    public long UserEventCount { get; set; }
    public long ProgramEventCount { get; set; }

    public long TotalDatabaseRelatedBytes => DatabaseBytes + WalBytes + ShmBytes;
    public long TotalRecordCount => ActivityEventCount + UserEventCount + ProgramEventCount;
}

public sealed class MaintenanceExclusionCleanupResult
{
    public long ActivityRecordsDeleted { get; set; }
    public long ProgramRecordsDeleted { get; set; }
    public long TotalRecordsDeleted => ActivityRecordsDeleted + ProgramRecordsDeleted;
}

public sealed class MaintenanceStatisticRow
{
    public int Rank { get; set; }
    public long Count { get; set; }
    public string Value { get; set; } = "";
    public string Extra { get; set; } = "";
    public string FirstSeen { get; set; } = "";
    public string LastSeen { get; set; } = "";
}

public sealed class ActivityEventRecord
{
    public string ActivityType { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Extension { get; set; } = "";
    public DateTime? OpenedTime { get; set; }
    public long? SizeAtOpening { get; set; }
    public DateTime? FirstWriteTime { get; set; }
    public DateTime? LastWriteTime { get; set; }
    public int WriteCount { get; set; }
    public long? SizeAtLastWrite { get; set; }
    public DateTime? ClosedTime { get; set; }
    public long? SizeAtClosing { get; set; }
    public string InferredAction { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public string Note { get; set; } = "";
}

public sealed class ExcelExportRow
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Extension { get; set; } = "";
    public string DateOpened { get; set; } = "";
    public string TimeOpened { get; set; } = "";
    public long? SizeAtOpening { get; set; }
    public string FirstWriteDate { get; set; } = "";
    public string FirstWriteTime { get; set; } = "";
    public string LastWriteDate { get; set; } = "";
    public string LastWriteTime { get; set; } = "";
    public long? WriteCount { get; set; }
    public long? SizeAtLastWrite { get; set; }
    public string DateClosed { get; set; } = "";
    public string TimeClosed { get; set; } = "";
    public long? SizeAtClosing { get; set; }
    public string InferredAction { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public long? ProcessId { get; set; }
    public string Note { get; set; } = "";
}

public sealed class ExportFieldOption
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string HeaderName { get; set; } = "";

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class ExportSheetOption
{
    public const string FileActivityId = "file-activity";
    public const string DailySummaryId = "daily-summary";
    public const string ExtensionSummaryId = "extension-summary";
    public const string ProcessSummaryId = "process-summary";
    public const string ErrorsId = "errors";
    public const string UserId = "user";
    public const string ProgramActivityId = "program-activity";

    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string WorksheetName { get; set; } = "";
    public List<string> FieldIds { get; set; } = new();

    public override string ToString()
    {
        return DisplayName;
    }

    public List<ExportFieldOption> GetAvailableFields()
    {
        return GetAvailableFields(Id);
    }

    public List<ExportFieldOption> GetSelectedFields()
    {
        return NormalizeFieldList(Id, FieldIds);
    }

    public static List<ExportSheetOption> GetAllOptions()
    {
        return new List<ExportSheetOption>
        {
            CreateOption(FileActivityId, "File Activity", "File Activity"),
            CreateOption(DailySummaryId, "Daily Summary", "Daily Summary"),
            CreateOption(ExtensionSummaryId, "Summary by Extension", "By Extension"),
            CreateOption(ProcessSummaryId, "Summary by Process", "By Process"),
            CreateOption(ErrorsId, "Errors", "Errors"),
            CreateOption(UserId, "User", "User"),
            CreateOption(ProgramActivityId, "Programs", "Programs")
        };
    }

    public static List<ExportSheetOption> GetDefaultOptions()
    {
        return GetAllOptions()
            .Where(option => option.Id == FileActivityId)
            .ToList();
    }

    private static ExportSheetOption CreateOption(string id, string displayName, string worksheetName)
    {
        return new ExportSheetOption
        {
            Id = id,
            DisplayName = displayName,
            WorksheetName = worksheetName,
            FieldIds = GetDefaultFieldIds(id)
        };
    }

    public static List<ExportFieldOption> GetAvailableFields(string sheetId)
    {
        if (string.Equals(sheetId, DailySummaryId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("date", "Date", "Date"),
                Field("events", "Events", "Events"),
                Field("open-events", "Open Events", "Open Events"),
                Field("write-events", "Write Events", "Write Events"),
                Field("close-events", "Close Events", "Close Events"),
                Field("error-events", "Error Events", "Error Events"),
                Field("distinct-files", "Distinct Files", "Distinct Files"),
                Field("distinct-processes", "Distinct Processes", "Distinct Processes")
            };
        }

        if (string.Equals(sheetId, ExtensionSummaryId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("extension", "File Type / Extension", "Extension"),
                Field("events", "Events", "Events"),
                Field("distinct-files", "Distinct Files", "Distinct Files"),
                Field("write-events", "Write Events", "Write Events"),
                Field("last-event", "Last Event", "Last Event")
            };
        }

        if (string.Equals(sheetId, ProcessSummaryId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("process", "Process", "Process"),
                Field("process-id", "Process ID", "Process ID"),
                Field("events", "Events", "Events"),
                Field("distinct-files", "Distinct Files", "Distinct Files"),
                Field("write-events", "Write Events", "Write Events"),
                Field("last-event", "Last Event", "Last Event")
            };
        }

        if (string.Equals(sheetId, ErrorsId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("id", "Id", "Id"),
                Field("created-at", "Created At", "Created At"),
                Field("item", "Item / Full Path", "Item"),
                Field("process", "Process", "Process"),
                Field("process-id", "Process ID", "Process ID"),
                Field("note", "Note", "Note")
            };
        }

        if (string.Equals(sheetId, UserId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("id", "Id", "Id"),
                Field("created-at", "Created At", "Created At"),
                Field("event-date", "Date", "Date"),
                Field("event-time", "Time", "Time"),
                Field("event-type", "Event Type", "Event Type"),
                Field("event-description", "Event", "Event"),
                Field("user-name", "User", "User"),
                Field("machine-name", "Computer", "Computer"),
                Field("process-name", "Process", "Process"),
                Field("process-id", "Process ID", "Process ID"),
                Field("app-version", "App Version", "App Version"),
                Field("note", "Note", "Note")
            };
        }

        if (string.Equals(sheetId, ProgramActivityId, StringComparison.OrdinalIgnoreCase))
        {
            return new List<ExportFieldOption>
            {
                Field("id", "Id", "Id"),
                Field("created-at", "Created At", "Created At"),
                Field("event-date", "Date", "Date"),
                Field("event-time", "Time", "Time"),
                Field("event-type", "Event Type", "Event Type"),
                Field("event-description", "Event", "Event"),
                Field("program-name", "Program", "Program"),
                Field("process-id", "Process ID", "Process ID"),
                Field("file-path", "Program Path", "Program Path"),
                Field("window-title", "Window Title", "Window Title"),
                Field("user-name", "User", "User"),
                Field("machine-name", "Computer", "Computer"),
                Field("app-version", "App Version", "App Version"),
                Field("note", "Note", "Note")
            };
        }

        return new List<ExportFieldOption>
        {
            Field("id", "Id", "Id"),
            Field("created-at", "Created At", "Created At"),
            Field("activity-type", "Activity Type", "Activity Type"),
            Field("full-path", "Full Path", "Full Path"),
            Field("folder-path", "Folder Path", "Folder Path"),
            Field("file-name", "File Name", "File Name"),
            Field("extension", "File Type / Extension", "Extension"),
            Field("date-opened", "Date Opened", "Date Opened"),
            Field("time-opened", "Time Opened", "Time Opened"),
            Field("size-at-opening", "Size At Opening", "Size At Opening"),
            Field("first-write-date", "First Write Date", "First Write Date"),
            Field("first-write-time", "First Write Time", "First Write Time"),
            Field("last-write-date", "Last Write Date", "Last Write Date"),
            Field("last-write-time", "Last Write Time", "Last Write Time"),
            Field("write-count", "Write Count", "Write Count"),
            Field("size-at-last-write", "Size At Last Write", "Size At Last Write"),
            Field("date-closed", "Date Closed", "Date Closed"),
            Field("time-closed", "Time Closed", "Time Closed"),
            Field("size-at-closing", "Size At Closing", "Size At Closing"),
            Field("inferred-action", "Inferred Action", "Inferred Action"),
            Field("process-name", "Process", "Process"),
            Field("process-id", "Process ID", "Process ID"),
            Field("note", "Note", "Note")
        };
    }

    public static List<string> GetDefaultFieldIds(string sheetId)
    {
        return GetAvailableFields(sheetId).Select(field => field.Id).ToList();
    }

    public static List<ExportFieldOption> NormalizeFieldList(string sheetId, IReadOnlyList<string>? fieldIds)
    {
        var availableFields = GetAvailableFields(sheetId);

        if (fieldIds == null || fieldIds.Count == 0)
            return availableFields;

        var result = new List<ExportFieldOption>();

        foreach (var fieldId in fieldIds)
        {
            var field = availableFields.FirstOrDefault(x => string.Equals(x.Id, fieldId, StringComparison.OrdinalIgnoreCase));

            if (field == null)
                continue;

            if (result.Any(x => string.Equals(x.Id, field.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(field);
        }

        if (result.Count == 0)
            return availableFields;

        return result;
    }

    public static List<ExportSheetOption> ParseList(string text)
    {
        var allOptions = GetAllOptions();
        var result = new List<ExportSheetOption>();

        if (!string.IsNullOrWhiteSpace(text))
        {
            var entries = text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var entry in entries)
            {
                var sheetId = entry;
                var fieldsPart = "";
                var separatorIndex = entry.IndexOf(':');

                if (separatorIndex >= 0)
                {
                    sheetId = entry.Substring(0, separatorIndex).Trim();
                    fieldsPart = entry.Substring(separatorIndex + 1).Trim();
                }

                var template = allOptions.FirstOrDefault(x => string.Equals(x.Id, sheetId, StringComparison.OrdinalIgnoreCase));

                if (template == null)
                    continue;

                if (result.Any(x => string.Equals(x.Id, template.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var selectedFieldIds = string.IsNullOrWhiteSpace(fieldsPart)
                    ? GetDefaultFieldIds(template.Id)
                    : fieldsPart.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                result.Add(new ExportSheetOption
                {
                    Id = template.Id,
                    DisplayName = template.DisplayName,
                    WorksheetName = template.WorksheetName,
                    FieldIds = NormalizeFieldList(template.Id, selectedFieldIds).Select(field => field.Id).ToList()
                });
            }
        }

        if (result.Count == 0)
            result = GetDefaultOptions();

        return result;
    }

    public static List<ExportSheetOption> NormalizeList(IReadOnlyList<ExportSheetOption>? options)
    {
        if (options == null || options.Count == 0)
            return GetDefaultOptions();

        var allOptions = GetAllOptions();
        var result = new List<ExportSheetOption>();

        foreach (var option in options)
        {
            var matched = allOptions.FirstOrDefault(x => string.Equals(x.Id, option.Id, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
                continue;

            if (result.Any(x => string.Equals(x.Id, matched.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new ExportSheetOption
            {
                Id = matched.Id,
                DisplayName = matched.DisplayName,
                WorksheetName = matched.WorksheetName,
                FieldIds = NormalizeFieldList(matched.Id, option.FieldIds).Select(field => field.Id).ToList()
            });
        }

        if (result.Count == 0)
            result = GetDefaultOptions();

        return result;
    }

    public static string ToRegistryText(IReadOnlyList<ExportSheetOption> options)
    {
        return string.Join(",", NormalizeList(options).Select(option => option.Id + ":" + string.Join("|", NormalizeFieldList(option.Id, option.FieldIds).Select(field => field.Id))));
    }

    private static ExportFieldOption Field(string id, string displayName, string headerName)
    {
        return new ExportFieldOption
        {
            Id = id,
            DisplayName = displayName,
            HeaderName = headerName
        };
    }
}

public sealed class UserEventRecord
{
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = "";
    public string EventDescription { get; set; } = "";
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public string AppVersion { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class UserExportRow
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string EventTime { get; set; } = "";
    public string EventType { get; set; } = "";
    public string EventDescription { get; set; } = "";
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public long? ProcessId { get; set; }
    public string AppVersion { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class ProgramEventRecord
{
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = "";
    public string EventDescription { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public int ProcessId { get; set; }
    public string FilePath { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class ProgramExportRow
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string EventTime { get; set; } = "";
    public string EventType { get; set; } = "";
    public string EventDescription { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public long? ProcessId { get; set; }
    public string FilePath { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class FileOpenInfo
{
    public DateTime OpenedTime { get; set; }
    public long? SizeAtOpening { get; set; }
    public DateTime? FirstWriteTime { get; set; }
    public long? SizeAtFirstWrite { get; set; }
    public DateTime? LastWriteTime { get; set; }
    public long? SizeAtLastWrite { get; set; }
    public int WriteCount { get; set; }
}

public static class PathUtilities
{
    public static string NormalizeEtwPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "";

        var trimmed = filePath.Trim();

        var lanmanPrefix = @"\;LanmanRedirector\;";

        if (trimmed.StartsWith(lanmanPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = trimmed.Substring(lanmanPrefix.Length);

            var firstSlash = rest.IndexOf('\\');

            if (firstSlash > 0)
            {
                var driveToken = rest.Substring(0, firstSlash);
                var colonIndex = driveToken.IndexOf(':');

                if (colonIndex > 0)
                {
                    var driveLetter = driveToken.Substring(0, colonIndex);
                    var afterDriveToken = rest.Substring(firstSlash + 1);

                    var secondSlash = afterDriveToken.IndexOf('\\');

                    if (secondSlash >= 0)
                    {
                        var pathAfterServer = afterDriveToken.Substring(secondSlash + 1);

                        if (!string.IsNullOrWhiteSpace(pathAfterServer))
                            return driveLetter + @":\" + pathAfterServer;
                    }
                }
            }
        }

        return trimmed;
    }

    public static string GetFolderPath(string fullPath)
    {
        try
        {
            var folder = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrWhiteSpace(folder))
                return "";

            return folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }
        catch
        {
            return "";
        }
    }

    public static string GetFileNameOnly(string fullPath)
    {
        try
        {
            return Path.GetFileName(fullPath) ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static string GetExtension(string fullPath)
    {
        try
        {
            var extension = Path.GetExtension(fullPath);

            if (string.IsNullOrWhiteSpace(extension))
                return "";

            return extension.ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }
}


public sealed class ExclusionRule
{
    public bool IsInclude { get; set; }
    public string RuleType { get; set; } = "folder";
    public bool IncludeSubfolders { get; set; } = true;
    public string Value { get; set; } = "";

    public bool IsExclude => !IsInclude;
}

public static class ExclusionRuleParser
{
    public static string BuildRule(string ruleType, string value, bool isInclude, bool includeSubfolders)
    {
        ruleType = (ruleType ?? "").Trim().ToLowerInvariant();

        if (ruleType.Equals("process", StringComparison.OrdinalIgnoreCase))
            return BuildProcessRule(value, isInclude);

        if (ruleType.Equals("file", StringComparison.OrdinalIgnoreCase))
            return BuildFileRule(value, isInclude);

        return BuildFolderRule(value, isInclude, includeSubfolders);
    }

    public static ExclusionRule ParseRule(string value)
    {
        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return new ExclusionRule { IsInclude = false, RuleType = "folder", IncludeSubfolders = true, Value = "" };

        var parts = value.Split('|', 4);

        if (parts.Length == 4 &&
            (parts[0].Equals("include", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("exclude", StringComparison.OrdinalIgnoreCase)))
        {
            if (parts[1].Equals("process", StringComparison.OrdinalIgnoreCase))
                return ParseProcessRule(value);

            if (parts[1].Equals("folder", StringComparison.OrdinalIgnoreCase))
                return ParseFolderRule(value);

            if (parts[1].Equals("file", StringComparison.OrdinalIgnoreCase))
                return ParseFileRule(value);
        }

        // Backward compatibility: plain entries are old folder exclusions unless parsed specifically as process rules.
        return ParseFolderRule(value);
    }

    public static string BuildFolderRule(string folderPath, bool isInclude, bool includeSubfolders)
    {
        folderPath = (folderPath ?? "").Trim();

        if (string.IsNullOrWhiteSpace(folderPath))
            return "";

        return (isInclude ? "include" : "exclude") + "|folder|" + (includeSubfolders ? "recursive" : "folder-only") + "|" + folderPath;
    }

    public static string BuildFileRule(string filePath, bool isInclude)
    {
        filePath = (filePath ?? "").Trim();

        if (string.IsNullOrWhiteSpace(filePath))
            return "";

        return (isInclude ? "include" : "exclude") + "|file||" + filePath;
    }

    public static string BuildProcessRule(string processName, bool isInclude)
    {
        processName = (processName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(processName))
            return "";

        return (isInclude ? "include" : "exclude") + "|process||" + processName;
    }

    public static ExclusionRule ParseFolderRule(string value)
    {
        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return new ExclusionRule { IsInclude = false, RuleType = "folder", IncludeSubfolders = true, Value = "" };

        var parts = value.Split('|', 4);

        if (parts.Length == 4 &&
            (parts[0].Equals("include", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("exclude", StringComparison.OrdinalIgnoreCase)) &&
            parts[1].Equals("folder", StringComparison.OrdinalIgnoreCase))
        {
            return new ExclusionRule
            {
                IsInclude = parts[0].Equals("include", StringComparison.OrdinalIgnoreCase),
                RuleType = "folder",
                IncludeSubfolders = !parts[2].Equals("folder-only", StringComparison.OrdinalIgnoreCase),
                Value = parts[3].Trim()
            };
        }

        // Backward compatibility with the earlier 0.1.3.0 folder suffix format.
        var older = PathExclusions.ParseExcludedFolderRule(value);
        return new ExclusionRule
        {
            IsInclude = false,
            RuleType = "folder",
            IncludeSubfolders = older.IncludeSubfolders,
            Value = older.FolderPath
        };
    }

    public static ExclusionRule ParseFileRule(string value)
    {
        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return new ExclusionRule { IsInclude = false, RuleType = "file", IncludeSubfolders = false, Value = "" };

        var parts = value.Split('|', 4);

        if (parts.Length == 4 &&
            (parts[0].Equals("include", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("exclude", StringComparison.OrdinalIgnoreCase)) &&
            parts[1].Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            return new ExclusionRule
            {
                IsInclude = parts[0].Equals("include", StringComparison.OrdinalIgnoreCase),
                RuleType = "file",
                IncludeSubfolders = false,
                Value = parts[3].Trim()
            };
        }

        return new ExclusionRule
        {
            IsInclude = false,
            RuleType = "file",
            IncludeSubfolders = false,
            Value = value
        };
    }

    public static ExclusionRule ParseProcessRule(string value)
    {
        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return new ExclusionRule { IsInclude = false, RuleType = "process", IncludeSubfolders = false, Value = "" };

        var parts = value.Split('|', 4);

        if (parts.Length == 4 &&
            (parts[0].Equals("include", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("exclude", StringComparison.OrdinalIgnoreCase)) &&
            parts[1].Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            return new ExclusionRule
            {
                IsInclude = parts[0].Equals("include", StringComparison.OrdinalIgnoreCase),
                RuleType = "process",
                IncludeSubfolders = false,
                Value = parts[3].Trim()
            };
        }

        // Existing process exclusions are exclude rules.
        return new ExclusionRule
        {
            IsInclude = false,
            RuleType = "process",
            IncludeSubfolders = false,
            Value = value
        };
    }
}

public static class PathExclusions
{
    private const string FolderOnlySuffix = "|folder-only";
    private const string RecursiveSuffix = "|recursive";

    public static bool IsInExcludedFolder(string filePath, IEnumerable<string> excludedFolders)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var folder in excludedFolders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                var rule = ExclusionRuleParser.ParseFolderRule(folder);

                if (string.IsNullOrWhiteSpace(rule.Value))
                    continue;

                if (FolderRuleMatches(fullPath, rule))
                    return rule.IsExclude;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool FileRuleMatches(string fullPath, ExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedRule = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.Value))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedPath.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool FolderRuleMatches(string fullPath, ExclusionRule rule)
    {
        try
        {
            var fullRulePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.Value))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (fullPath.Equals(fullRulePath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (rule.IncludeSubfolders && fullPath.StartsWith(fullRulePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static string BuildExcludedFolderRule(string folderPath, bool includeSubfolders)
    {
        return ExclusionRuleParser.BuildFolderRule(folderPath, false, includeSubfolders);
    }

    public static string BuildFolderRule(string folderPath, bool isInclude, bool includeSubfolders)
    {
        return ExclusionRuleParser.BuildFolderRule(folderPath, isInclude, includeSubfolders);
    }

    public static (string FolderPath, bool IncludeSubfolders) ParseExcludedFolderRule(string value)
    {
        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return ("", true);

        if (value.EndsWith(FolderOnlySuffix, StringComparison.OrdinalIgnoreCase))
            return (value[..^FolderOnlySuffix.Length].Trim(), false);

        if (value.EndsWith(RecursiveSuffix, StringComparison.OrdinalIgnoreCase))
            return (value[..^RecursiveSuffix.Length].Trim(), true);

        // Existing exclusions from earlier versions did not have a suffix.
        // Keep the old behaviour: a plain folder path also excludes subfolders.
        return (value, true);
    }

    public static bool IsInTempFolder(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var tempFolders = new[]
            {
                Path.GetTempPath(),
                Environment.GetEnvironmentVariable("TEMP") ?? "",
                Environment.GetEnvironmentVariable("TMP") ?? "",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            foreach (var folder in tempFolders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                var fullFolder = Path.GetFullPath(folder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (fullPath.Equals(fullFolder, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

public static class ProcessExclusions
{
    public static bool IsExcludedProcess(string processName, IEnumerable<string> excludedProcesses)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var normalized = NormalizeProcessName(processName);

        foreach (var excluded in excludedProcesses)
        {
            if (string.IsNullOrWhiteSpace(excluded))
                continue;

            var rule = ExclusionRuleParser.ParseProcessRule(excluded);

            if (string.IsNullOrWhiteSpace(rule.Value))
                continue;

            if (normalized.Equals(NormalizeProcessName(rule.Value), StringComparison.OrdinalIgnoreCase))
                return rule.IsExclude;
        }

        return false;
    }

    private static string NormalizeProcessName(string processName)
    {
        var normalized = (processName ?? "").Trim();

        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];

        return normalized;
    }

}

public static class LoggingRulesEngine
{
    public static bool IsFileActivityExcluded(string fullPath, string processName, IEnumerable<string> loggingRules)
    {
        foreach (var ruleText in loggingRules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);

            if (string.IsNullOrWhiteSpace(rule.Value))
                continue;

            var matches = rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase)
                ? ProcessRuleMatches(processName, rule)
                : rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase)
                    ? FileRuleMatches(fullPath, rule)
                    : FolderRuleMatches(fullPath, rule);

            if (matches)
                return rule.IsExclude;
        }

        return false;
    }

    public static bool IsProgramActivityExcluded(string filePath, string processName, IEnumerable<string> loggingRules)
    {
        foreach (var ruleText in loggingRules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);

            if (string.IsNullOrWhiteSpace(rule.Value))
                continue;

            var matches = rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase)
                ? ProcessRuleMatches(processName, rule)
                : rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase)
                    ? FileRuleMatches(filePath, rule)
                    : FolderRuleMatches(filePath, rule);

            if (matches)
                return rule.IsExclude;
        }

        return false;
    }

    private static bool ProcessRuleMatches(string processName, ExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        return NormalizeProcessName(processName).Equals(NormalizeProcessName(rule.Value), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProcessName(string processName)
    {
        var normalized = (processName ?? "").Trim();

        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];

        return normalized;
    }

    private static bool FileRuleMatches(string fullPath, ExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedRule = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.Value))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedPath.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool FolderRuleMatches(string fullPath, ExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedRule = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.Value))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (normalizedPath.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase))
                return true;

            if (rule.IncludeSubfolders && normalizedPath.StartsWith(normalizedRule + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }

}


public static class FileExclusions
{
    public static bool IsExactExcludedFile(string filePath, IEnumerable<string> excludedFiles)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(filePath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var file in excludedFiles ?? Array.Empty<string>())
            {
                var rule = ExclusionRuleParser.ParseRule(file);

                if (!rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rule.Value) || rule.IsInclude)
                    continue;

                var normalizedRule = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.Value))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normalizedPath.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}

public static class StartupTaskManager
{
    private const string TaskName = "DeskPulse";

    public static bool IsEnabled()
    {
        try
        {
            using var process = StartHiddenProcess("schtasks.exe", $"/Query /TN \"{TaskName}\"");
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            CreateTask();
        else
            DeleteTask();
    }

    private static void CreateTask()
    {
        var executablePath = Application.ExecutablePath;

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            throw new InvalidOperationException("DeskPulse could not determine the executable path for Windows startup.");

        var taskCommand = $"\\\"{executablePath}\\\"";
        var arguments = $"/Create /TN \"{TaskName}\" /TR \"{taskCommand}\" /SC ONLOGON /RL HIGHEST /F";

        RunSchtasks(arguments, "create the Windows startup task");
    }

    private static void DeleteTask()
    {
        if (!IsEnabled())
            return;

        RunSchtasks($"/Delete /TN \"{TaskName}\" /F", "remove the Windows startup task");
    }

    private static void RunSchtasks(string arguments, string actionDescription)
    {
        using var process = StartHiddenProcess("schtasks.exe", arguments);
        process.WaitForExit(15000);

        if (process.ExitCode != 0)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            var details = string.Join(Environment.NewLine, new[] { output, error }.Where(x => !string.IsNullOrWhiteSpace(x)));

            throw new InvalidOperationException(
                $"DeskPulse could not {actionDescription}." +
                (string.IsNullOrWhiteSpace(details) ? "" : Environment.NewLine + Environment.NewLine + details.Trim()));
        }
    }

    private static Process StartHiddenProcess(string fileName, string arguments)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Could not start schtasks.exe.");
    }
}

public sealed class AppSettings
{
    private const string RegistryPath = @"Software\DeskPulse";

    public string DataFolderPath { get; set; } = GetDefaultDataFolderPath();

    public bool IgnoreTempFolders { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool LogProgramActivity { get; set; } = true;

    public List<string> LoggingRules { get; set; } = GetDefaultLoggingRules();

    public List<string> ExcludedFolders { get; set; } = GetDefaultExcludedFolders();

    public HashSet<string> ExcludedProcesses { get; set; } = GetDefaultExcludedProcesses();

    public HashSet<string> ExtensionsToMonitor { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".pdf",
        ".docx",
        ".xlsx",
        ".dwg",
        ".jpg",
        ".png",
        ".cs"
    };

    public List<ExportSheetOption> ExportSheets { get; set; } = ExportSheetOption.GetDefaultOptions();

    public string DatabaseFilePath => Path.Combine(DataFolderPath, "DeskPulse.db");

    public string ExcelExportFilePath => Path.Combine(DataFolderPath, "DeskPulse-export.xlsx");

    public string ExtensionsAsText =>
        string.Join(", ", ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DataFolderPath = DataFolderPath,
            IgnoreTempFolders = IgnoreTempFolders,
            StartWithWindows = StartWithWindows,
            LogProgramActivity = LogProgramActivity,
            LoggingRules = new List<string>(LoggingRules),
            ExcludedFolders = new List<string>(ExcludedFolders),
            ExcludedProcesses = new HashSet<string>(ExcludedProcesses, StringComparer.OrdinalIgnoreCase),
            ExtensionsToMonitor = new HashSet<string>(ExtensionsToMonitor, StringComparer.OrdinalIgnoreCase),
            ExportSheets = ExportSheetOption.NormalizeList(ExportSheets)
        };
    }

    public static AppSettings Load()
    {
        var settings = new AppSettings();

        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);

        if (key == null)
        {
            settings.Save();
            return settings;
        }

        settings.DataFolderPath = ReadString(key, "DataFolderPath", "");

        if (string.IsNullOrWhiteSpace(settings.DataFolderPath))
        {
            var oldLogFilePath = ReadString(key, "LogFilePath", "");

            if (!string.IsNullOrWhiteSpace(oldLogFilePath))
            {
                var oldFolder = Path.GetDirectoryName(oldLogFilePath);

                settings.DataFolderPath = string.IsNullOrWhiteSpace(oldFolder)
                    ? GetDefaultDataFolderPath()
                    : oldFolder;
            }
            else
            {
                settings.DataFolderPath = GetDefaultDataFolderPath();
            }
        }

        settings.IgnoreTempFolders = ReadBool(key, "IgnoreTempFolders", true);
        settings.StartWithWindows = ReadBool(key, "StartWithWindows", StartupTaskManager.IsEnabled());
        settings.LogProgramActivity = ReadBool(key, "LogProgramActivity", true);

        var excludedFoldersText = ReadString(key, "ExcludedFolders", ToMultilineText(settings.ExcludedFolders));
        settings.ExcludedFolders = ParsePlainList(excludedFoldersText);

        var excludedProcessesText = ReadString(key, "ExcludedProcesses", ToMultilineText(settings.ExcludedProcesses));
        settings.ExcludedProcesses = ParsePlainSet(excludedProcessesText);

        var loggingRulesText = ReadString(key, "LoggingRules", "");
        settings.LoggingRules = ParsePlainListPreserveOrder(loggingRulesText);

        if (settings.LoggingRules.Count == 0)
            settings.LoggingRules = BuildLoggingRulesFromLegacy(settings.ExcludedFolders, settings.ExcludedProcesses);

        var exportSheetsText = ReadString(key, "ExportSheets", ExportSheetOption.ToRegistryText(settings.ExportSheets));
        settings.ExportSheets = ExportSheetOption.ParseList(exportSheetsText);

        var extensionsText = ReadString(key, "ExtensionsToMonitor", settings.ExtensionsAsText);
        settings.ExtensionsToMonitor = ParseExtensions(extensionsText);

        if (settings.ExtensionsToMonitor.Count == 0)
        {
            settings.ExtensionsToMonitor = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".txt",
                ".pdf",
                ".docx",
                ".xlsx",
                ".dwg",
                ".jpg",
                ".png",
                ".cs"
            };
        }

        if (settings.ExcludedFolders.Count == 0)
            settings.ExcludedFolders = GetDefaultExcludedFolders();

        if (settings.ExcludedProcesses.Count == 0)
            settings.ExcludedProcesses = GetDefaultExcludedProcesses();

        if (settings.LoggingRules.Count == 0)
            settings.LoggingRules = GetDefaultLoggingRules();

        settings.Save();

        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);

        if (key == null)
            return;

        key.SetValue("AppVersion", AppInfo.Version, RegistryValueKind.String);
        key.SetValue("DataFolderPath", DataFolderPath, RegistryValueKind.String);
        key.SetValue("DatabaseFilePath", DatabaseFilePath, RegistryValueKind.String);
        key.SetValue("ExcelExportFilePath", ExcelExportFilePath, RegistryValueKind.String);
        key.SetValue("ExtensionsToMonitor", ExtensionsAsText, RegistryValueKind.String);
        key.SetValue("ExportSheets", ExportSheetOption.ToRegistryText(ExportSheets), RegistryValueKind.String);
        key.SetValue("IgnoreTempFolders", IgnoreTempFolders ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("StartWithWindows", StartWithWindows ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("LogProgramActivity", LogProgramActivity ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("LoggingRules", ToOrderedMultilineText(LoggingRules), RegistryValueKind.String);
        key.SetValue("ExcludedFolders", ToOrderedMultilineText(ExcludedFolders), RegistryValueKind.String);
        key.SetValue("ExcludedProcesses", ToMultilineText(ExcludedProcesses), RegistryValueKind.String);
    }

    public static List<string> GetDefaultLoggingRules()
    {
        return BuildLoggingRulesFromLegacy(GetDefaultExcludedFolders(), GetDefaultExcludedProcesses());
    }

    public static List<string> BuildLoggingRulesFromLegacy(IEnumerable<string> folderRules, IEnumerable<string> processRules)
    {
        var result = new List<string>();

        foreach (var folder in folderRules ?? Array.Empty<string>())
        {
            var parsed = ExclusionRuleParser.ParseFolderRule(folder);
            if (!string.IsNullOrWhiteSpace(parsed.Value))
                result.Add(ExclusionRuleParser.BuildFolderRule(parsed.Value, parsed.IsInclude, parsed.IncludeSubfolders));
        }

        foreach (var process in processRules ?? Array.Empty<string>())
        {
            var parsed = ExclusionRuleParser.ParseProcessRule(process);
            if (!string.IsNullOrWhiteSpace(parsed.Value))
                result.Add(ExclusionRuleParser.BuildProcessRule(parsed.Value, parsed.IsInclude));
        }

        return result;
    }

    public static List<string> GetDefaultExcludedFolders()
    {
        var result = new List<string>();

        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(windowsFolder))
        {
            result.Add(Path.Combine(windowsFolder, "Temp"));
            result.Add(Path.Combine(windowsFolder, "SoftwareDistribution"));
            result.Add(Path.Combine(windowsFolder, "Prefetch"));
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            result.Add(Path.Combine(localAppData, "Temp"));
            result.Add(Path.Combine(localAppData, "Microsoft", "Windows", "INetCache"));
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static HashSet<string> GetDefaultExcludedProcesses()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SearchIndexer",
            "SearchProtocolHost",
            "SearchFilterHost",
            "MsMpEng",
            "CompatTelRunner",
            "svchost",
            "RuntimeBroker"
        };
    }

    public static string ToOrderedMultilineText(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, (values ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()));
    }

    public static string ToMultilineText(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    public static List<string> ParsePlainListPreserveOrder(string text)
    {
        return ParsePlainValues(text)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
    }

    public static List<string> ParsePlainList(string text)
    {
        return ParsePlainValues(text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static HashSet<string> ParsePlainSet(string text)
    {
        return new HashSet<string>(ParsePlainValues(text), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParsePlainValues(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var part in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var value = part.Trim();

            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    public static string GetRegistryPathForDisplay()
    {
        return @"HKCU\" + RegistryPath;
    }

    public static void DeleteRegistrySettings()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
        }
        catch (ArgumentException)
        {
            // The key does not exist. Treat this as successful cleanup.
        }
    }

    public static HashSet<string> ParseExtensions(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return result;

        var parts = text
            .Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));

        foreach (var part in parts)
        {
            var extension = part.StartsWith(".", StringComparison.Ordinal)
                ? part
                : "." + part;

            result.Add(extension.ToLowerInvariant());
        }

        return result;
    }

    public static string GetDefaultDataFolderPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DeskPulse"
        );
    }

    private static string ReadString(RegistryKey key, string name, string fallback)
    {
        try
        {
            return key.GetValue(name)?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool ReadBool(RegistryKey key, string name, bool fallback)
    {
        try
        {
            var value = key.GetValue(name);

            if (value == null)
                return fallback;

            if (value is int intValue)
                return intValue != 0;

            if (value is string stringValue)
            {
                if (bool.TryParse(stringValue, out var boolValue))
                    return boolValue;

                if (int.TryParse(stringValue, out var parsedInt))
                    return parsedInt != 0;
            }

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

