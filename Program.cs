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
    public const string Version = "0.1.0";
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
        MaintenanceModeEnabled = HasSwitch(args, "maintenance");
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

            foreach (var process in startedProcesses)
                WriteProgramEvent("ProgramStarted", "Program started", process, process.StartTime ?? DateTime.Now, "Detected in the current interactive Windows session");

            foreach (var process in stoppedProcesses)
                WriteProgramEvent("ProgramStopped", "Program closed", process, DateTime.Now, "Process no longer detected in the current interactive Windows session");
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

public static class PathExclusions
{
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

public sealed class SettingsForm : Form
{
    private readonly TextBox _dataFolderTextBox = new();
    private readonly ListBox _availableExtensionsListBox = new();
    private readonly ListBox _monitoredExtensionsListBox = new();
    private readonly CheckBox _ignoreTempFoldersCheckBox = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly CheckBox _logProgramActivityCheckBox = new();
    private readonly CheckedListBox _exportSheetsCheckedListBox = new();
    private readonly TabControl _exportFieldTabControl = new();
    private readonly Dictionary<string, CheckedListBox> _exportFieldListsBySheetId = new(StringComparer.OrdinalIgnoreCase);
    private bool _updatingExportUi;

    public SettingsForm()
    {
        Text = "DeskPulse Settings";
        ClientSize = new System.Drawing.Size(920, 690);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        BackColor = System.Drawing.SystemColors.Control;

        var settings = AppSettings.Load();

        var tabControl = new TabControl
        {
            Left = 16,
            Top = 16,
            Width = 888,
            Height = 600
        };

        var generalTab = CreateTabPage("General");
        var filesTab = CreateTabPage("Files");
        var exportOptionsTab = CreateTabPage("Export Options");

        tabControl.TabPages.Add(generalTab);
        tabControl.TabPages.Add(filesTab);
        tabControl.TabPages.Add(exportOptionsTab);

        BuildGeneralTab(generalTab, settings);
        BuildFilesTab(filesTab, settings);
        BuildExportOptionsTab(exportOptionsTab, settings);

        if (AppRuntime.MaintenanceModeEnabled)
        {
            var maintenanceTab = CreateTabPage("Maintenance");
            tabControl.TabPages.Add(maintenanceTab);
            BuildMaintenanceTab(maintenanceTab, settings);
        }

        var footerLine = new Label
        {
            Left = 16,
            Top = 626,
            Width = 888,
            Height = 1,
            BorderStyle = BorderStyle.Fixed3D
        };

        var okButton = CreateActionButton("Save", 724, 644, 84);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => SaveSettings();

        var cancelButton = CreateActionButton("Cancel", 820, 644, 84);
        cancelButton.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            tabControl,
            footerLine,
            okButton,
            cancelButton
        });

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadExtensionLists(settings);
    }

    private TabPage CreateTabPage(string text)
    {
        return new TabPage
        {
            Text = text,
            BackColor = System.Drawing.SystemColors.Window,
            Padding = new Padding(16)
        };
    }

    private GroupBox CreateGroupBox(string text, int left, int top, int width, int height)
    {
        return new GroupBox
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Padding = new Padding(12),
            BackColor = System.Drawing.SystemColors.Window
        };
    }

    private static Label CreateHintLabel(string text, int left, int top, int width, int height)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            ForeColor = System.Drawing.SystemColors.GrayText
        };
    }

    private Button CreateActionButton(string text, int left, int top, int width)
    {
        return new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.System
        };
    }

    private void BuildGeneralTab(TabPage generalTab, AppSettings settings)
    {
        var startupGroup = CreateGroupBox("Windows startup", 24, 24, 820, 150);

        _startWithWindowsCheckBox.Left = 18;
        _startWithWindowsCheckBox.Top = 32;
        _startWithWindowsCheckBox.Width = 520;
        _startWithWindowsCheckBox.Text = "Start DeskPulse when I log in to Windows";
        _startWithWindowsCheckBox.Checked = settings.StartWithWindows || StartupTaskManager.IsEnabled();

        var startupHintLabel = CreateHintLabel(
            "Creates a current-user Windows Task Scheduler entry and starts DeskPulse with highest privileges when you log in.",
            40,
            62,
            740,
            22);

        var quietStartupLabel = CreateHintLabel(
            "DeskPulse starts quietly in the tray. Startup errors are still shown if monitoring cannot start.",
            40,
            88,
            740,
            22);

        startupGroup.Controls.AddRange(new Control[]
        {
            _startWithWindowsCheckBox,
            startupHintLabel,
            quietStartupLabel
        });

        var behaviourGroup = CreateGroupBox("Application behaviour", 24, 196, 820, 150);

        _logProgramActivityCheckBox.Left = 18;
        _logProgramActivityCheckBox.Top = 32;
        _logProgramActivityCheckBox.Width = 520;
        _logProgramActivityCheckBox.Text = "Log program start and close activity";
        _logProgramActivityCheckBox.Checked = settings.LogProgramActivity;

        var programActivityHintLabel = CreateHintLabel(
            "Records programs that start and close in the current interactive Windows session. This is not a full system-service audit log.",
            40,
            62,
            740,
            36);

        var behaviourLabel = CreateHintLabel(
            "DeskPulse keeps running from the tray icon. Left-click opens Export/Settings; right-click opens About/Exit.",
            18,
            106,
            760,
            22);

        behaviourGroup.Controls.AddRange(new Control[]
        {
            _logProgramActivityCheckBox,
            programActivityHintLabel,
            behaviourLabel
        });

        generalTab.Controls.AddRange(new Control[]
        {
            startupGroup,
            behaviourGroup
        });
    }


    private void BuildFilesTab(TabPage filesTab, AppSettings settings)
    {
        var storageGroup = CreateGroupBox("Storage", 24, 24, 820, 122);

        var dataFolderLabel = new Label
        {
            Text = "Data folder",
            Left = 18,
            Top = 34,
            Width = 90
        };

        _dataFolderTextBox.Left = 112;
        _dataFolderTextBox.Top = 30;
        _dataFolderTextBox.Width = 560;
        _dataFolderTextBox.Text = settings.DataFolderPath;

        var browseButton = CreateActionButton("Browse...", 686, 28, 100);
        browseButton.Click += (_, _) => BrowseForDataFolder();

        var databaseLabel = CreateHintLabel(
            "Live data: DeskPulse.db    Export report: DeskPulse-export.xlsx",
            112,
            62,
            650,
            22);

        storageGroup.Controls.AddRange(new Control[]
        {
            dataFolderLabel,
            _dataFolderTextBox,
            browseButton,
            databaseLabel
        });

        var filterGroup = CreateGroupBox("File activity filters", 24, 166, 820, 88);

        _ignoreTempFoldersCheckBox.Left = 18;
        _ignoreTempFoldersCheckBox.Top = 30;
        _ignoreTempFoldersCheckBox.Width = 360;
        _ignoreTempFoldersCheckBox.Text = "Ignore temporary-folder activity";
        _ignoreTempFoldersCheckBox.Checked = settings.IgnoreTempFolders;

        var tempHintLabel = CreateHintLabel(
            "Recommended. Turn off only when you deliberately want to include Windows temp-folder activity.",
            40,
            56,
            720,
            22);

        filterGroup.Controls.AddRange(new Control[]
        {
            _ignoreTempFoldersCheckBox,
            tempHintLabel
        });

        var fileTypesGroup = CreateGroupBox("Monitored file types", 24, 274, 820, 280);

        var availableLabel = new Label
        {
            Text = "Available file types",
            Left = 18,
            Top = 28,
            Width = 300
        };

        _availableExtensionsListBox.Left = 18;
        _availableExtensionsListBox.Top = 52;
        _availableExtensionsListBox.Width = 330;
        _availableExtensionsListBox.Height = 190;
        _availableExtensionsListBox.DisplayMember = nameof(RegisteredFileTypeInfo.DisplayName);
        _availableExtensionsListBox.SelectionMode = SelectionMode.One;
        _availableExtensionsListBox.IntegralHeight = false;
        _availableExtensionsListBox.MouseDown += ListBoxMouseDown;
        _availableExtensionsListBox.AllowDrop = true;
        _availableExtensionsListBox.DragEnter += ListBoxDragEnter;
        _availableExtensionsListBox.DragDrop += (_, e) => RemoveDraggedExtension(e);

        var monitoredLabel = new Label
        {
            Text = "Currently monitored",
            Left = 472,
            Top = 28,
            Width = 300
        };

        _monitoredExtensionsListBox.Left = 472;
        _monitoredExtensionsListBox.Top = 52;
        _monitoredExtensionsListBox.Width = 330;
        _monitoredExtensionsListBox.Height = 190;
        _monitoredExtensionsListBox.SelectionMode = SelectionMode.One;
        _monitoredExtensionsListBox.IntegralHeight = false;
        _monitoredExtensionsListBox.MouseDown += ListBoxMouseDown;
        _monitoredExtensionsListBox.AllowDrop = true;
        _monitoredExtensionsListBox.DragEnter += ListBoxDragEnter;
        _monitoredExtensionsListBox.DragDrop += (_, e) => AddDraggedExtension(e);

        var addButton = CreateActionButton("Add  >", 366, 95, 88);
        addButton.Click += (_, _) => AddSelectedExtension();

        var removeButton = CreateActionButton("<  Remove", 366, 135, 88);
        removeButton.Click += (_, _) => RemoveSelectedExtension();

        var dragHintLabel = CreateHintLabel(
            "You can also drag file types between the lists.",
            18,
            246,
            760,
            22);

        fileTypesGroup.Controls.AddRange(new Control[]
        {
            availableLabel,
            _availableExtensionsListBox,
            addButton,
            removeButton,
            monitoredLabel,
            _monitoredExtensionsListBox,
            dragHintLabel
        });

        filesTab.Controls.AddRange(new Control[]
        {
            storageGroup,
            filterGroup,
            fileTypesGroup
        });
    }


    private void BuildExportOptionsTab(TabPage exportOptionsTab, AppSettings settings)
    {
        var introLabel = CreateHintLabel(
            "Choose which Excel worksheets DeskPulse creates, then choose the columns and column order for each selected worksheet.",
            24,
            22,
            820,
            28);

        var worksheetGroup = CreateGroupBox("Worksheets", 24, 62, 300, 492);

        _exportSheetsCheckedListBox.Left = 18;
        _exportSheetsCheckedListBox.Top = 30;
        _exportSheetsCheckedListBox.Width = 262;
        _exportSheetsCheckedListBox.Height = 350;
        _exportSheetsCheckedListBox.DisplayMember = nameof(ExportSheetOption.DisplayName);
        _exportSheetsCheckedListBox.CheckOnClick = true;
        _exportSheetsCheckedListBox.SelectionMode = SelectionMode.One;
        _exportSheetsCheckedListBox.IntegralHeight = false;
        _exportSheetsCheckedListBox.ItemCheck += (_, _) =>
        {
            if (_updatingExportUi)
                return;

            if (IsHandleCreated)
                BeginInvoke(new Action(RebuildExportFieldTabs));
            else
                RebuildExportFieldTabs();
        };

        var sheetUpButton = CreateActionButton("Move Up", 18, 394, 82);
        sheetUpButton.Click += (_, _) => MoveSelectedExportSheet(-1);

        var sheetDownButton = CreateActionButton("Move Down", 108, 394, 92);
        sheetDownButton.Click += (_, _) => MoveSelectedExportSheet(1);

        var resetButton = CreateActionButton("Reset", 208, 394, 72);
        resetButton.Click += (_, _) => ResetExportSheetsToDefault();

        var worksheetHintLabel = CreateHintLabel(
            "Ticked items become workbook tabs. Their order here becomes the Excel worksheet order.",
            18,
            430,
            260,
            44);

        worksheetGroup.Controls.AddRange(new Control[]
        {
            _exportSheetsCheckedListBox,
            sheetUpButton,
            sheetDownButton,
            resetButton,
            worksheetHintLabel
        });

        var fieldsGroup = CreateGroupBox("Worksheet columns", 344, 62, 500, 492);

        _exportFieldTabControl.Left = 18;
        _exportFieldTabControl.Top = 30;
        _exportFieldTabControl.Width = 462;
        _exportFieldTabControl.Height = 350;

        var fieldUpButton = CreateActionButton("Move Up", 18, 394, 82);
        fieldUpButton.Click += (_, _) => MoveSelectedExportField(-1);

        var fieldDownButton = CreateActionButton("Move Down", 108, 394, 92);
        fieldDownButton.Click += (_, _) => MoveSelectedExportField(1);

        var selectAllFieldsButton = CreateActionButton("Select All", 216, 394, 86);
        selectAllFieldsButton.Click += (_, _) => SetCurrentExportFieldChecks(true);

        var clearFieldsButton = CreateActionButton("Clear", 310, 394, 74);
        clearFieldsButton.Click += (_, _) => SetCurrentExportFieldChecks(false);

        var hintLabel = CreateHintLabel(
            "The order shown in each field list becomes the column order in that worksheet.",
            18,
            430,
            450,
            44);

        fieldsGroup.Controls.AddRange(new Control[]
        {
            _exportFieldTabControl,
            fieldUpButton,
            fieldDownButton,
            selectAllFieldsButton,
            clearFieldsButton,
            hintLabel
        });

        exportOptionsTab.Controls.AddRange(new Control[]
        {
            introLabel,
            worksheetGroup,
            fieldsGroup
        });

        LoadExportOptions(settings);
    }


    private void BuildMaintenanceTab(TabPage maintenanceTab, AppSettings settings)
    {
        var introLabel = CreateHintLabel(
            "Maintenance tools are visible only when DeskPulse is started with -maintenance.",
            24,
            22,
            820,
            28);

        var startupGroup = CreateGroupBox("Windows startup", 24, 62, 820, 82);

        var autostartLabel = CreateHintLabel(
            "The normal startup option is now on the General tab.",
            18,
            34,
            760,
            22);

        startupGroup.Controls.Add(autostartLabel);

        var foldersGroup = CreateGroupBox("Folders", 24, 164, 820, 96);

        var openDataFolderButton = CreateActionButton("Open Data Folder", 18, 36, 170);
        openDataFolderButton.Click += (_, _) => OpenFolder(settings.DataFolderPath, "DeskPulse data folder");

        var openProgramFolderButton = CreateActionButton("Open Program Folder", 202, 36, 180);
        openProgramFolderButton.Click += (_, _) => OpenFolder(AppContext.BaseDirectory, "DeskPulse program folder");

        foldersGroup.Controls.AddRange(new Control[]
        {
            openDataFolderButton,
            openProgramFolderButton
        });

        var diagnosticsGroup = CreateGroupBox("Diagnostics", 24, 280, 820, 118);

        var diagnosticsStatusLabel = CreateHintLabel(
            AppRuntime.DebugLoggingEnabled
                ? "Diagnostic logging is on. Normal -debug logs accepted monitored events only; add -debug-skipped only for full skip tracing."
                : "Diagnostic logging is off. Start DeskPulse with -debug to record accepted monitored ETW file events.",
            18,
            30,
            760,
            34);

        diagnosticsStatusLabel.ForeColor = AppRuntime.DebugLoggingEnabled
            ? System.Drawing.SystemColors.ControlText
            : System.Drawing.SystemColors.GrayText;

        var openDiagnosticsLogButton = CreateActionButton("Open Diagnostic Log", 18, 70, 170);
        openDiagnosticsLogButton.Click += (_, _) => OpenFile(DiagnosticLogger.GetDiagnosticLogFilePath(), "DeskPulse diagnostic log");

        var activeExtensionsButton = CreateActionButton("Show Active Extensions", 202, 70, 180);
        activeExtensionsButton.Click += (_, _) => ShowActiveExtensions();

        diagnosticsGroup.Controls.AddRange(new Control[]
        {
            diagnosticsStatusLabel,
            openDiagnosticsLogButton,
            activeExtensionsButton
        });

        var cleanupGroup = CreateGroupBox("Cleanup", 24, 418, 820, 136);

        var registryPathLabel = CreateHintLabel(
            "Registry settings location: " + AppSettings.GetRegistryPathForDisplay(),
            18,
            30,
            760,
            22);

        var removeRegistrySettingsButton = CreateActionButton("Remove Registry Settings", 18, 68, 190);
        removeRegistrySettingsButton.Click += (_, _) => RemoveRegistrySettings();

        var cleanupHintLabel = CreateHintLabel(
            "Removes current-user settings only. It does not delete the app, database, Excel export, or other files.",
            226,
            70,
            550,
            44);

        cleanupGroup.Controls.AddRange(new Control[]
        {
            registryPathLabel,
            removeRegistrySettingsButton,
            cleanupHintLabel
        });

        maintenanceTab.Controls.AddRange(new Control[]
        {
            introLabel,
            startupGroup,
            foldersGroup,
            diagnosticsGroup,
            cleanupGroup
        });
    }



    private static void OpenFile(string filePath, string fileDescription)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("The file path is empty.");

            var folder = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(filePath))
                File.WriteAllText(filePath, "DeskPulse diagnostic log has not recorded entries yet." + Environment.NewLine);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {fileDescription} could not be opened.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void ShowActiveExtensions()
    {
        var settings = AppSettings.Load();
        var extensions = string.Join(Environment.NewLine, settings.ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        MessageBox.Show(
            "Active monitored extensions:" + Environment.NewLine + Environment.NewLine + extensions,
            "DeskPulse Diagnostics",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private static void OpenFolder(string folderPath, string folderDescription)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new InvalidOperationException("The folder path is empty.");

            Directory.CreateDirectory(folderPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {folderDescription} could not be opened.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void RemoveRegistrySettings()
    {
        var confirm = MessageBox.Show(
            "This will remove DeskPulse settings from the Windows registry for the current user.\n\n" +
            "It will not delete the DeskPulse program files.\n" +
            "It will not delete the SQLite database or Excel export.\n\n" +
            "If you click OK in the Settings window afterwards, settings may be created again. " +
            "For a clean portable-app reset, close DeskPulse after removing the registry settings.\n\n" +
            "Continue?",
            "Remove DeskPulse Registry Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            AppSettings.DeleteRegistrySettings();

            MessageBox.Show(
                "DeskPulse registry settings were removed for the current Windows user.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"DeskPulse registry settings could not be removed.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void LoadExtensionLists(AppSettings settings)
    {
        var registered = RegisteredFileTypeReader.ReadRegisteredFileTypes();

        foreach (var item in registered)
            _availableExtensionsListBox.Items.Add(item);

        foreach (var extension in settings.ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            _monitoredExtensionsListBox.Items.Add(extension);
    }

    private void LoadExportOptions(AppSettings settings)
    {
        _updatingExportUi = true;

        try
        {
            _exportSheetsCheckedListBox.Items.Clear();
            _exportFieldTabControl.TabPages.Clear();
            _exportFieldListsBySheetId.Clear();

            var allOptions = ExportSheetOption.GetAllOptions();
            var selectedOptions = ExportSheetOption.NormalizeList(settings.ExportSheets);
            var orderedOptions = new List<ExportSheetOption>();

            foreach (var selected in selectedOptions)
            {
                var template = allOptions.FirstOrDefault(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));

                if (template == null)
                    continue;

                orderedOptions.Add(new ExportSheetOption
                {
                    Id = template.Id,
                    DisplayName = template.DisplayName,
                    WorksheetName = template.WorksheetName,
                    FieldIds = selected.FieldIds.ToList()
                });
            }

            foreach (var option in allOptions)
            {
                if (orderedOptions.Any(x => string.Equals(x.Id, option.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                orderedOptions.Add(option);
            }

            foreach (var option in orderedOptions)
            {
                var selectedOption = selectedOptions.FirstOrDefault(x => string.Equals(x.Id, option.Id, StringComparison.OrdinalIgnoreCase));
                var item = selectedOption ?? option;
                var itemIndex = _exportSheetsCheckedListBox.Items.Add(item);
                _exportSheetsCheckedListBox.SetItemChecked(itemIndex, selectedOption != null);
            }
        }
        finally
        {
            _updatingExportUi = false;
        }

        RebuildExportFieldTabs();
    }

    private void RebuildExportFieldTabs()
    {
        if (_updatingExportUi)
            return;

        var previousSelectedSheetId = "";

        if (_exportFieldTabControl.SelectedTab?.Tag is string selectedSheetId)
            previousSelectedSheetId = selectedSheetId;

        var existingFieldSelections = ReadCurrentFieldSelections();

        _exportFieldTabControl.TabPages.Clear();
        _exportFieldListsBySheetId.Clear();

        foreach (var option in GetCheckedExportSheetItems())
        {
            var fieldList = new CheckedListBox
            {
                Left = 8,
                Top = 8,
                Width = 430,
                Height = 300,
                CheckOnClick = true,
                SelectionMode = SelectionMode.One,
                IntegralHeight = false,
                DisplayMember = nameof(ExportFieldOption.DisplayName)
            };

            var fieldIds = existingFieldSelections.TryGetValue(option.Id, out var savedFieldIds)
                ? savedFieldIds
                : option.FieldIds;

            var selectedFields = ExportSheetOption.NormalizeFieldList(option.Id, fieldIds);
            var availableFields = option.GetAvailableFields();
            var orderedFields = new List<ExportFieldOption>();

            foreach (var selectedField in selectedFields)
            {
                var field = availableFields.FirstOrDefault(x => string.Equals(x.Id, selectedField.Id, StringComparison.OrdinalIgnoreCase));

                if (field != null && !orderedFields.Any(x => string.Equals(x.Id, field.Id, StringComparison.OrdinalIgnoreCase)))
                    orderedFields.Add(field);
            }

            foreach (var availableField in availableFields)
            {
                if (!orderedFields.Any(x => string.Equals(x.Id, availableField.Id, StringComparison.OrdinalIgnoreCase)))
                    orderedFields.Add(availableField);
            }

            foreach (var field in orderedFields)
            {
                var checkedIndex = fieldList.Items.Add(field);
                var isChecked = selectedFields.Any(x => string.Equals(x.Id, field.Id, StringComparison.OrdinalIgnoreCase));
                fieldList.SetItemChecked(checkedIndex, isChecked);
            }

            var tabPage = new TabPage
            {
                Text = option.WorksheetName,
                Tag = option.Id
            };

            tabPage.Controls.Add(fieldList);
            _exportFieldTabControl.TabPages.Add(tabPage);
            _exportFieldListsBySheetId[option.Id] = fieldList;

            if (string.Equals(previousSelectedSheetId, option.Id, StringComparison.OrdinalIgnoreCase))
                _exportFieldTabControl.SelectedTab = tabPage;
        }
    }

    private List<ExportSheetOption> GetCheckedExportSheetItems()
    {
        var result = new List<ExportSheetOption>();

        for (var i = 0; i < _exportSheetsCheckedListBox.Items.Count; i++)
        {
            if (!_exportSheetsCheckedListBox.GetItemChecked(i))
                continue;

            if (_exportSheetsCheckedListBox.Items[i] is ExportSheetOption option)
                result.Add(option);
        }

        return result;
    }

    private Dictionary<string, List<string>> ReadCurrentFieldSelections()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in _exportFieldListsBySheetId)
            result[pair.Key] = GetCheckedFieldIds(pair.Value);

        return result;
    }

    private static List<string> GetCheckedFieldIds(CheckedListBox fieldList)
    {
        var result = new List<string>();

        for (var i = 0; i < fieldList.Items.Count; i++)
        {
            if (!fieldList.GetItemChecked(i))
                continue;

            if (fieldList.Items[i] is ExportFieldOption field)
                result.Add(field.Id);
        }

        return result;
    }

    private void MoveSelectedExportSheet(int direction)
    {
        var selectedIndex = _exportSheetsCheckedListBox.SelectedIndex;

        if (selectedIndex < 0)
            return;

        var newIndex = selectedIndex + direction;

        if (newIndex < 0 || newIndex >= _exportSheetsCheckedListBox.Items.Count)
            return;

        var item = _exportSheetsCheckedListBox.Items[selectedIndex];
        var wasChecked = _exportSheetsCheckedListBox.GetItemChecked(selectedIndex);
        var otherWasChecked = _exportSheetsCheckedListBox.GetItemChecked(newIndex);

        _updatingExportUi = true;

        try
        {
            _exportSheetsCheckedListBox.Items.RemoveAt(selectedIndex);
            _exportSheetsCheckedListBox.Items.Insert(newIndex, item);
            _exportSheetsCheckedListBox.SetItemChecked(newIndex, wasChecked);

            if (selectedIndex < _exportSheetsCheckedListBox.Items.Count)
                _exportSheetsCheckedListBox.SetItemChecked(selectedIndex, otherWasChecked);

            _exportSheetsCheckedListBox.SelectedIndex = newIndex;
        }
        finally
        {
            _updatingExportUi = false;
        }

        RebuildExportFieldTabs();
    }

    private void MoveSelectedExportField(int direction)
    {
        var fieldList = GetCurrentFieldList();

        if (fieldList == null)
            return;

        var selectedIndex = fieldList.SelectedIndex;

        if (selectedIndex < 0)
            return;

        var newIndex = selectedIndex + direction;

        if (newIndex < 0 || newIndex >= fieldList.Items.Count)
            return;

        var item = fieldList.Items[selectedIndex];
        var wasChecked = fieldList.GetItemChecked(selectedIndex);
        var otherWasChecked = fieldList.GetItemChecked(newIndex);

        fieldList.Items.RemoveAt(selectedIndex);
        fieldList.Items.Insert(newIndex, item);
        fieldList.SetItemChecked(newIndex, wasChecked);

        if (selectedIndex < fieldList.Items.Count)
            fieldList.SetItemChecked(selectedIndex, otherWasChecked);

        fieldList.SelectedIndex = newIndex;
    }

    private void SetCurrentExportFieldChecks(bool isChecked)
    {
        var fieldList = GetCurrentFieldList();

        if (fieldList == null)
            return;

        for (var i = 0; i < fieldList.Items.Count; i++)
            fieldList.SetItemChecked(i, isChecked);
    }

    private CheckedListBox? GetCurrentFieldList()
    {
        if (_exportFieldTabControl.SelectedTab?.Tag is not string sheetId)
            return null;

        return _exportFieldListsBySheetId.TryGetValue(sheetId, out var fieldList)
            ? fieldList
            : null;
    }

    private void ResetExportSheetsToDefault()
    {
        var resetSettings = new AppSettings
        {
            ExportSheets = ExportSheetOption.GetDefaultOptions()
        };

        LoadExportOptions(resetSettings);
    }

    private List<ExportSheetOption> GetSelectedExportSheetsFromList()
    {
        var result = new List<ExportSheetOption>();
        var currentFieldSelections = ReadCurrentFieldSelections();

        foreach (var option in GetCheckedExportSheetItems())
        {
            var fieldIds = currentFieldSelections.TryGetValue(option.Id, out var selectedFieldIds)
                ? selectedFieldIds
                : option.FieldIds;

            result.Add(new ExportSheetOption
            {
                Id = option.Id,
                DisplayName = option.DisplayName,
                WorksheetName = option.WorksheetName,
                FieldIds = fieldIds
            });
        }

        return ExportSheetOption.NormalizeList(result);
    }

    private void BrowseForDataFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the DeskPulse data folder",
            SelectedPath = Directory.Exists(_dataFolderTextBox.Text.Trim())
                ? _dataFolderTextBox.Text.Trim()
                : AppSettings.GetDefaultDataFolderPath(),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            _dataFolderTextBox.Text = dialog.SelectedPath;
    }

    private void AddSelectedExtension()
    {
        if (_availableExtensionsListBox.SelectedItem is RegisteredFileTypeInfo info)
            AddExtensionToMonitoredList(info.Extension);
    }

    private void RemoveSelectedExtension()
    {
        var selected = _monitoredExtensionsListBox.SelectedItem;

        if (selected != null)
            _monitoredExtensionsListBox.Items.Remove(selected);
    }

    private void AddExtensionToMonitoredList(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return;

        if (!extension.StartsWith(".", StringComparison.Ordinal))
            extension = "." + extension;

        extension = extension.ToLowerInvariant();

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            if (string.Equals(item.ToString(), extension, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _monitoredExtensionsListBox.Items.Add(extension);
    }

    private HashSet<string> GetMonitoredExtensionsFromList()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            var text = item.ToString();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var extension = text.Trim();

            if (!extension.StartsWith(".", StringComparison.Ordinal))
                extension = "." + extension;

            result.Add(extension.ToLowerInvariant());
        }

        return result;
    }

    private void ListBoxMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        var index = listBox.IndexFromPoint(e.Location);

        if (index < 0)
            return;

        var item = listBox.Items[index];

        if (item == null)
            return;

        listBox.DoDragDrop(item, DragDropEffects.Move);
    }

    private void ListBoxDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && (e.Data.GetDataPresent(typeof(RegisteredFileTypeInfo)) || e.Data.GetDataPresent(typeof(string))))
        {
            e.Effect = DragDropEffects.Move;
            return;
        }

        e.Effect = DragDropEffects.None;
    }

    private void AddDraggedExtension(DragEventArgs e)
    {
        if (e.Data == null)
            return;

        if (e.Data.GetData(typeof(RegisteredFileTypeInfo)) is RegisteredFileTypeInfo info)
        {
            AddExtensionToMonitoredList(info.Extension);
            return;
        }

        if (e.Data.GetData(typeof(string)) is string text)
            AddExtensionToMonitoredList(text);
    }

    private void RemoveDraggedExtension(DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(string)) is not string text)
            return;

        foreach (var item in _monitoredExtensionsListBox.Items.Cast<object>().ToList())
        {
            if (string.Equals(item.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                _monitoredExtensionsListBox.Items.Remove(item);
                return;
            }
        }
    }

    private void SaveSettings()
    {
        var dataFolder = _dataFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(dataFolder))
        {
            MessageBox.Show(
                "Please enter a data folder.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        if (!Path.IsPathFullyQualified(dataFolder))
        {
            MessageBox.Show(
                "Please enter a full data folder path, for example:\n\nC:\\Users\\YourName\\Documents\\DeskPulse",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            Directory.CreateDirectory(dataFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The data folder could not be created or accessed.\n\n{ex.Message}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            DialogResult = DialogResult.None;
            return;
        }

        var extensions = GetMonitoredExtensionsFromList();

        if (extensions.Count == 0)
        {
            MessageBox.Show(
                "Please add at least one monitored file extension.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var exportSheets = GetSelectedExportSheetsFromList();

        if (exportSheets.Count == 0)
        {
            MessageBox.Show(
                "Please select at least one Excel export worksheet.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var startWithWindows = _startWithWindowsCheckBox.Checked;

        try
        {
            StartupTaskManager.SetEnabled(startWithWindows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The Windows startup setting could not be saved.\n\n" + ex.Message,
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            DialogResult = DialogResult.None;
            return;
        }

        var settings = new AppSettings
        {
            DataFolderPath = dataFolder,
            IgnoreTempFolders = _ignoreTempFoldersCheckBox.Checked,
            StartWithWindows = startWithWindows,
            LogProgramActivity = _logProgramActivityCheckBox.Checked,
            ExtensionsToMonitor = extensions,
            ExportSheets = exportSheets
        };

        settings.Save();
    }
}

public sealed class RegisteredFileTypeInfo
{
    public string Extension { get; set; } = "";
    public string Description { get; set; } = "";

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Description))
                return Extension;

            return $"{Extension} - {Description}";
        }
    }

    public override string ToString()
    {
        return Extension;
    }
}

public static class RegisteredFileTypeReader
{
    public static List<RegisteredFileTypeInfo> ReadRegisteredFileTypes()
    {
        var result = new List<RegisteredFileTypeInfo>();

        try
        {
            foreach (var subKeyName in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!subKeyName.StartsWith(".", StringComparison.Ordinal))
                    continue;

                if (subKeyName.Length < 2)
                    continue;

                var description = "";

                try
                {
                    using var extensionKey = Registry.ClassesRoot.OpenSubKey(subKeyName);
                    var className = extensionKey?.GetValue(null)?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(className))
                    {
                        using var classKey = Registry.ClassesRoot.OpenSubKey(className);
                        description = classKey?.GetValue(null)?.ToString() ?? "";
                    }
                }
                catch
                {
                    description = "";
                }

                result.Add(new RegisteredFileTypeInfo
                {
                    Extension = subKeyName.ToLowerInvariant(),
                    Description = description
                });
            }
        }
        catch
        {
            // Return whatever could be read.
        }

        return result
            .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class ExportDateRangeForm : Form
{
    private readonly Action<DateTime, DateTime, IProgress<ExportProgressInfo>> _exportAction;
    private readonly MonthCalendar _startCalendar = new();
    private readonly MonthCalendar _endCalendar = new();
    private readonly Button _exportButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _progressLabel = new();

    public DateTime StartDate => _startCalendar.SelectionStart.Date;
    public DateTime EndDate => _endCalendar.SelectionStart.Date;

    public ExportDateRangeForm(Action<DateTime, DateTime, IProgress<ExportProgressInfo>> exportAction)
    {
        _exportAction = exportAction ?? throw new ArgumentNullException(nameof(exportAction));

        Text = "Export Activity Log";
        ClientSize = new System.Drawing.Size(560, 450);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        BackColor = System.Drawing.SystemColors.Window;

        var title = new Label
        {
            Text = "Export activity log",
            Left = 24,
            Top = 22,
            Width = 500,
            Height = 28,
            Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point)
        };

        var hint = new Label
        {
            Text = "Choose the first and last day to include. Both calendars default to today.",
            Left = 24,
            Top = 56,
            Width = 500,
            Height = 34,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var startGroup = new GroupBox
        {
            Text = "Start day",
            Left = 24,
            Top = 104,
            Width = 248,
            Height = 198
        };

        _startCalendar.Left = 12;
        _startCalendar.Top = 22;
        _startCalendar.MaxSelectionCount = 1;
        _startCalendar.SelectionStart = DateTime.Today;
        _startCalendar.SelectionEnd = DateTime.Today;
        _startCalendar.ShowTodayCircle = true;

        startGroup.Controls.Add(_startCalendar);

        var endGroup = new GroupBox
        {
            Text = "End day",
            Left = 288,
            Top = 104,
            Width = 248,
            Height = 198
        };

        _endCalendar.Left = 12;
        _endCalendar.Top = 22;
        _endCalendar.MaxSelectionCount = 1;
        _endCalendar.SelectionStart = DateTime.Today;
        _endCalendar.SelectionEnd = DateTime.Today;
        _endCalendar.ShowTodayCircle = true;

        endGroup.Controls.Add(_endCalendar);

        _progressLabel.Text = "0%   Waiting to export";
        _progressLabel.Left = 24;
        _progressLabel.Top = 368;
        _progressLabel.Width = 500;
        _progressLabel.Height = 22;
        _progressLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _progressLabel.Visible = false;

        _progressBar.Left = 24;
        _progressBar.Top = 338;
        _progressBar.Width = 512;
        _progressBar.Height = 20;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Visible = false;

        var separator = new Label
        {
            Left = 24,
            Top = 398,
            Width = 512,
            Height = 1,
            BorderStyle = BorderStyle.Fixed3D
        };

        _exportButton.Text = "Export";
        _exportButton.Left = 360;
        _exportButton.Top = 412;
        _exportButton.Width = 80;
        _exportButton.Height = 30;
        _exportButton.FlatStyle = FlatStyle.System;
        _exportButton.Click += async (_, _) => await ExportAsync();

        _cancelButton.Text = "Cancel";
        _cancelButton.Left = 456;
        _cancelButton.Top = 412;
        _cancelButton.Width = 80;
        _cancelButton.Height = 30;
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.FlatStyle = FlatStyle.System;

        Controls.AddRange(new Control[]
        {
            title,
            hint,
            startGroup,
            endGroup,
            _progressLabel,
            _progressBar,
            separator,
            _exportButton,
            _cancelButton
        });

        AcceptButton = _exportButton;
        CancelButton = _cancelButton;
    }

    private async Task ExportAsync()
    {
        if (EndDate < StartDate)
        {
            MessageBox.Show(
                "The end day cannot be before the start day.",
                "Invalid export date range",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        SetExportInProgress(true);

        try
        {
            var startDate = StartDate;
            var endDate = EndDate;
            var progress = new Progress<ExportProgressInfo>(UpdateExportProgress);

            await Task.Run(() => _exportAction(startDate, endDate, progress));

            UpdateExportProgress(new ExportProgressInfo(100, "100% Export complete"));

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SetExportInProgress(false);

            MessageBox.Show(
                ex.Message,
                "Could not export activity log",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetExportInProgress(bool inProgress)
    {
        _startCalendar.Enabled = !inProgress;
        _endCalendar.Enabled = !inProgress;
        _exportButton.Enabled = !inProgress;
        _cancelButton.Enabled = !inProgress;
        ControlBox = !inProgress;

        _exportButton.Text = inProgress ? "Exporting..." : "Export";
        _progressLabel.Visible = inProgress;
        _progressBar.Visible = inProgress;

        if (inProgress)
        {
            _progressBar.Value = 0;
            _progressLabel.Text = "0%   Waiting to export";
        }
    }

    private void UpdateExportProgress(ExportProgressInfo progressInfo)
    {
        var percent = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progressInfo.Percent));
        _progressBar.Value = percent;

        var message = string.IsNullOrWhiteSpace(progressInfo.Message)
            ? $"{percent}%   Exporting activity log"
            : progressInfo.Message;

        _progressLabel.Text = message;
    }
}

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About DeskPulse";
        ClientSize = new System.Drawing.Size(460, 270);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        BackColor = System.Drawing.SystemColors.Window;

        var title = new Label
        {
            Text = AppInfo.AppName,
            Left = 24,
            Top = 22,
            Width = 380,
            Height = 30,
            Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point)
        };

        var version = new Label
        {
            Text = "Version " + AppInfo.Version,
            Left = 26,
            Top = 54,
            Width = 380,
            Height = 22,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var description = new Label
        {
            Text = "DeskPulse quietly tracks selected file activity while you work. It helps you review what was opened, changed, or saved, and exports clear reports to Excel when needed.",
            Left = 26,
            Top = 92,
            Width = 395,
            Height = 62
        };

        var linkCaption = new Label
        {
            Text = "Project page",
            Left = 26,
            Top = 166,
            Width = 90,
            Height = 22,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var link = new LinkLabel
        {
            Text = AppInfo.GitHubUrl,
            Left = 116,
            Top = 166,
            Width = 305,
            Height = 22
        };

        link.Links.Add(0, AppInfo.GitHubUrl.Length, AppInfo.GitHubUrl);

        link.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        };

        var separator = new Label
        {
            Left = 24,
            Top = 208,
            Width = 412,
            Height = 1,
            BorderStyle = BorderStyle.Fixed3D
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 356,
            Top = 226,
            Width = 80,
            Height = 30,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.System
        };

        Controls.AddRange(new Control[]
        {
            title,
            version,
            description,
            linkCaption,
            link,
            separator,
            okButton
        });

        AcceptButton = okButton;
    }
}
