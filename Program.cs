using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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

        SQLitePCL.Batteries_V2.Init();

        Application.Run(new TrayAppContext());
    }
}

public static class AppInfo
{
    public const string AppName = "DeskPulse";
    public const string Version = "0.0.3";
    public const string GitHubUrl = "https://github.com/KaiEysselein/DeskPulse";
}

public static class AppRuntime
{
    public static bool DebugLoggingEnabled { get; private set; }
    public static bool DebugSkippedLoggingEnabled { get; private set; }

    public static void Configure(string[] args)
    {
        DebugLoggingEnabled = args.Any(arg =>
            string.Equals(arg, "-debug", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/debug", StringComparison.OrdinalIgnoreCase));

        DebugSkippedLoggingEnabled = args.Any(arg =>
            string.Equals(arg, "-debug-skipped", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--debug-skipped", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/debug-skipped", StringComparison.OrdinalIgnoreCase));
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

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
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

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open log file in Excel", null, (_, _) => OpenExcelReport());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettingsWindow());
        menu.Items.Add("About", null, (_, _) => OpenAboutWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon.ContextMenuStrip = menu;

        try
        {
            _monitor.Start();
            ShowBalloon(AppRuntime.DebugLoggingEnabled
                ? "Activity monitoring started. Diagnostic logging is enabled."
                : "Activity monitoring started.");
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
        try
        {
            _monitor.ExportAndOpenExcelReport();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Could not open Excel report",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void OpenSettingsWindow()
    {
        using var form = new SettingsForm();

        if (form.ShowDialog() == DialogResult.OK)
        {
            _monitor.ReloadSettings();
            ShowBalloon("Settings saved.");
        }
    }

    private static void OpenAboutWindow()
    {
        using var form = new AboutForm();
        form.ShowDialog();
    }

    private void ShowBalloon(string text)
    {
        _trayIcon.BalloonTipTitle = AppInfo.AppName;
        _trayIcon.BalloonTipText = text;
        _trayIcon.ShowBalloonTip(3000);
    }

    protected override void ExitThreadCore()
    {
        _monitor.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

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
    private TraceEventSession? _session;
    private Thread? _workerThread;
    private bool _disposed;

    public FileIoMonitor()
    {
        _settings = AppSettings.Load();

        EnsureDataFolderExists(_settings.DataFolderPath);

        _database = new DeskPulseDatabase(_settings.DatabaseFilePath);
        _database.Initialize();

        DiagnosticLogger.WriteStartupEntry(_settings);
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

            DiagnosticLogger.WriteStartupEntry(_settings);
        }
    }

    public void ExportAndOpenExcelReport()
    {
        AppSettings settings;
        DeskPulseDatabase database;

        lock (_settingsLock)
        {
            settings = _settings.Clone();
            database = _database;
        }

        EnsureDataFolderExists(settings.DataFolderPath);

        database.ExportToExcel(settings.ExcelExportFilePath);

        Process.Start(new ProcessStartInfo
        {
            FileName = settings.ExcelExportFilePath,
            UseShellExecute = true
        });
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

    public void ExportToExcel(string excelFilePath)
    {
        lock (_dbLock)
        {
            var folder = Path.GetDirectoryName(excelFilePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            var rows = ReadAllRows();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("File Activity");

            var headers = new[]
            {
                "Id",
                "Created At",
                "Activity Type",
                "Full Path",
                "Folder Path",
                "File Name",
                "Extension",
                "Date Opened",
                "Time Opened",
                "Size At Opening",
                "First Write Date",
                "First Write Time",
                "Last Write Date",
                "Last Write Time",
                "Write Count",
                "Size At Last Write",
                "Date Closed",
                "Time Closed",
                "Size At Closing",
                "Inferred Action",
                "Process",
                "Process ID",
                "Note"
            };

            for (var col = 0; col < headers.Length; col++)
            {
                sheet.Cell(1, col + 1).Value = headers[col];
                sheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            var rowNumber = 2;

            foreach (var row in rows)
            {
                sheet.Cell(rowNumber, 1).Value = row.Id;
                sheet.Cell(rowNumber, 2).Value = row.CreatedAt;
                sheet.Cell(rowNumber, 3).Value = row.ActivityType;
                sheet.Cell(rowNumber, 4).Value = row.FullPath;
                sheet.Cell(rowNumber, 5).Value = row.FolderPath;
                sheet.Cell(rowNumber, 6).Value = row.FileName;
                sheet.Cell(rowNumber, 7).Value = row.Extension;
                sheet.Cell(rowNumber, 8).Value = row.DateOpened;
                sheet.Cell(rowNumber, 9).Value = row.TimeOpened;
                SetCellValue(sheet.Cell(rowNumber, 10), row.SizeAtOpening);
                sheet.Cell(rowNumber, 11).Value = row.FirstWriteDate;
                sheet.Cell(rowNumber, 12).Value = row.FirstWriteTime;
                sheet.Cell(rowNumber, 13).Value = row.LastWriteDate;
                sheet.Cell(rowNumber, 14).Value = row.LastWriteTime;
                SetCellValue(sheet.Cell(rowNumber, 15), row.WriteCount);
                SetCellValue(sheet.Cell(rowNumber, 16), row.SizeAtLastWrite);
                sheet.Cell(rowNumber, 17).Value = row.DateClosed;
                sheet.Cell(rowNumber, 18).Value = row.TimeClosed;
                SetCellValue(sheet.Cell(rowNumber, 19), row.SizeAtClosing);
                sheet.Cell(rowNumber, 20).Value = row.InferredAction;
                sheet.Cell(rowNumber, 21).Value = row.ProcessName;
                SetCellValue(sheet.Cell(rowNumber, 22), row.ProcessId);
                sheet.Cell(rowNumber, 23).Value = row.Note;

                rowNumber++;
            }

            sheet.SheetView.FreezeRows(1);

            if (sheet.RangeUsed() != null)
                sheet.RangeUsed()!.SetAutoFilter();

            sheet.Columns().AdjustToContents(1, Math.Min(rowNumber, 250));

            var exportFolder = Path.GetDirectoryName(excelFilePath) ?? AppContext.BaseDirectory;
            var exportFileNameWithoutExtension = Path.GetFileNameWithoutExtension(excelFilePath);

            var tempFilePath = Path.Combine(
                exportFolder,
                $"{exportFileNameWithoutExtension}-{Guid.NewGuid():N}.xlsx"
            );

            workbook.SaveAs(tempFilePath);

            if (File.Exists(excelFilePath))
                File.Delete(excelFilePath);

            File.Move(tempFilePath, excelFilePath);
        }
    }

    private static void SetCellValue(IXLCell cell, long? value)
    {
        if (value == null)
            cell.Value = "";
        else
            cell.Value = value.Value;
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

public sealed class AppSettings
{
    private const string RegistryPath = @"Software\DeskPulse";

    public string DataFolderPath { get; set; } = GetDefaultDataFolderPath();

    public bool IgnoreTempFolders { get; set; } = true;

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
            ExtensionsToMonitor = new HashSet<string>(ExtensionsToMonitor, StringComparer.OrdinalIgnoreCase)
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
        key.SetValue("IgnoreTempFolders", IgnoreTempFolders ? 1 : 0, RegistryValueKind.DWord);
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

    public SettingsForm()
    {
        Text = "DeskPulse Settings";
        Width = 860;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var settings = AppSettings.Load();

        var tabControl = new TabControl
        {
            Left = 12,
            Top = 12,
            Width = 820,
            Height = 540
        };

        var filesTab = new TabPage
        {
            Text = "Files"
        };

        var maintenanceTab = new TabPage
        {
            Text = "Maintenance"
        };

        tabControl.TabPages.Add(filesTab);
        tabControl.TabPages.Add(maintenanceTab);

        BuildFilesTab(filesTab, settings);
        BuildMaintenanceTab(maintenanceTab, settings);

        var okButton = new Button
        {
            Text = "OK",
            Left = 650,
            Top = 565,
            Width = 80,
            DialogResult = DialogResult.OK
        };

        okButton.Click += (_, _) => SaveSettings();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 744,
            Top = 565,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            tabControl,
            okButton,
            cancelButton
        });

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadExtensionLists(settings);
    }

    private void BuildFilesTab(TabPage filesTab, AppSettings settings)
    {
        var dataFolderLabel = new Label
        {
            Text = "Data folder:",
            Left = 16,
            Top = 20,
            Width = 100
        };

        _dataFolderTextBox.Left = 120;
        _dataFolderTextBox.Top = 16;
        _dataFolderTextBox.Width = 560;
        _dataFolderTextBox.Text = settings.DataFolderPath;

        var browseButton = new Button
        {
            Text = "Browse...",
            Left = 690,
            Top = 14,
            Width = 95
        };

        browseButton.Click += (_, _) => BrowseForDataFolder();

        var databaseLabel = new Label
        {
            Text = "Database: DeskPulse.db   |   Excel export: DeskPulse-export.xlsx",
            Left = 120,
            Top = 45,
            Width = 620,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        _ignoreTempFoldersCheckBox.Left = 120;
        _ignoreTempFoldersCheckBox.Top = 72;
        _ignoreTempFoldersCheckBox.Width = 520;
        _ignoreTempFoldersCheckBox.Text = "Ignore temporary-folder activity";
        _ignoreTempFoldersCheckBox.Checked = settings.IgnoreTempFolders;

        var tempHintLabel = new Label
        {
            Text = "Recommended: keep this enabled. Turn it off only if you deliberately want to log file activity inside Windows temp folders.",
            Left = 145,
            Top = 96,
            Width = 620,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var availableLabel = new Label
        {
            Text = "Registered Windows file types:",
            Left = 16,
            Top = 130,
            Width = 300
        };

        _availableExtensionsListBox.Left = 16;
        _availableExtensionsListBox.Top = 152;
        _availableExtensionsListBox.Width = 350;
        _availableExtensionsListBox.Height = 250;
        _availableExtensionsListBox.DisplayMember = nameof(RegisteredFileTypeInfo.DisplayName);
        _availableExtensionsListBox.SelectionMode = SelectionMode.One;
        _availableExtensionsListBox.MouseDown += ListBoxMouseDown;
        _availableExtensionsListBox.AllowDrop = true;
        _availableExtensionsListBox.DragEnter += ListBoxDragEnter;
        _availableExtensionsListBox.DragDrop += (_, e) => RemoveDraggedExtension(e);

        var monitoredLabel = new Label
        {
            Text = "Monitored file types:",
            Left = 435,
            Top = 130,
            Width = 300
        };

        _monitoredExtensionsListBox.Left = 435;
        _monitoredExtensionsListBox.Top = 152;
        _monitoredExtensionsListBox.Width = 350;
        _monitoredExtensionsListBox.Height = 250;
        _monitoredExtensionsListBox.SelectionMode = SelectionMode.One;
        _monitoredExtensionsListBox.MouseDown += ListBoxMouseDown;
        _monitoredExtensionsListBox.AllowDrop = true;
        _monitoredExtensionsListBox.DragEnter += ListBoxDragEnter;
        _monitoredExtensionsListBox.DragDrop += (_, e) => AddDraggedExtension(e);

        var addButton = new Button
        {
            Text = "Add >",
            Left = 372,
            Top = 210,
            Width = 56
        };

        addButton.Click += (_, _) => AddSelectedExtension();

        var removeButton = new Button
        {
            Text = "< Remove",
            Left = 372,
            Top = 250,
            Width = 56
        };

        removeButton.Click += (_, _) => RemoveSelectedExtension();

        filesTab.Controls.AddRange(new Control[]
        {
            dataFolderLabel,
            _dataFolderTextBox,
            browseButton,
            databaseLabel,
            _ignoreTempFoldersCheckBox,
            tempHintLabel,
            availableLabel,
            _availableExtensionsListBox,
            addButton,
            removeButton,
            monitoredLabel,
            _monitoredExtensionsListBox
        });
    }

    private void BuildMaintenanceTab(TabPage maintenanceTab, AppSettings settings)
    {
        var headingLabel = new Label
        {
            Text = "Maintenance and portable-app cleanup",
            Left = 16,
            Top = 20,
            Width = 740,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        var explanationLabel = new Label
        {
            Text = "These options are for the portable version of DeskPulse. They do not use an installer and they do not delete the program folder automatically.",
            Left = 16,
            Top = 48,
            Width = 760,
            Height = 35,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var autostartHeadingLabel = new Label
        {
            Text = "Windows startup",
            Left = 16,
            Top = 95,
            Width = 300,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        var autostartCheckBox = new CheckBox
        {
            Text = "Start DeskPulse with Windows",
            Left = 32,
            Top = 122,
            Width = 300,
            Enabled = false
        };

        var autostartHintLabel = new Label
        {
            Text = "Coming in a future version. This will use Windows Task Scheduler so DeskPulse can start with the required Administrator privileges.",
            Left = 55,
            Top = 147,
            Width = 700,
            Height = 35,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var foldersHeadingLabel = new Label
        {
            Text = "Folders",
            Left = 16,
            Top = 198,
            Width = 300,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        var openDataFolderButton = new Button
        {
            Text = "Open DeskPulse Data Folder",
            Left = 32,
            Top = 225,
            Width = 230,
            Height = 30
        };

        openDataFolderButton.Click += (_, _) => OpenFolder(settings.DataFolderPath, "DeskPulse data folder");

        var openProgramFolderButton = new Button
        {
            Text = "Open DeskPulse Program Folder",
            Left = 280,
            Top = 225,
            Width = 230,
            Height = 30
        };

        openProgramFolderButton.Click += (_, _) => OpenFolder(AppContext.BaseDirectory, "DeskPulse program folder");

        var diagnosticsHeadingLabel = new Label
        {
            Text = "Diagnostics",
            Left = 16,
            Top = 285,
            Width = 300,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        var diagnosticsStatusLabel = new Label
        {
            Text = AppRuntime.DebugLoggingEnabled
                ? "Diagnostic logging is ON. Normal -debug logs accepted monitored events only. Add -debug-skipped only for full skip tracing."
                : "Diagnostic logging is OFF. Start DeskPulse with -debug to record accepted monitored ETW file events.",
            Left = 32,
            Top = 312,
            Width = 720,
            ForeColor = AppRuntime.DebugLoggingEnabled
                ? System.Drawing.SystemColors.ControlText
                : System.Drawing.SystemColors.GrayText
        };

        var openDiagnosticsLogButton = new Button
        {
            Text = "Open Diagnostic Log",
            Left = 32,
            Top = 342,
            Width = 200,
            Height = 30
        };

        openDiagnosticsLogButton.Click += (_, _) => OpenFile(DiagnosticLogger.GetDiagnosticLogFilePath(), "DeskPulse diagnostic log");

        var activeExtensionsButton = new Button
        {
            Text = "Show Active Extensions",
            Left = 250,
            Top = 342,
            Width = 200,
            Height = 30
        };

        activeExtensionsButton.Click += (_, _) => ShowActiveExtensions();

        var cleanupHeadingLabel = new Label
        {
            Text = "Cleanup",
            Left = 16,
            Top = 395,
            Width = 300,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        var registryPathLabel = new Label
        {
            Text = "Registry settings location: " + AppSettings.GetRegistryPathForDisplay(),
            Left = 32,
            Top = 422,
            Width = 720,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var removeRegistrySettingsButton = new Button
        {
            Text = "Remove DeskPulse Registry Settings",
            Left = 32,
            Top = 452,
            Width = 260,
            Height = 32
        };

        removeRegistrySettingsButton.Click += (_, _) => RemoveRegistrySettings();

        var cleanupHintLabel = new Label
        {
            Text = "This removes DeskPulse settings from the current Windows user only. It does not delete the app, database, Excel export, or other files.",
            Left = 310,
            Top = 452,
            Width = 450,
            Height = 45,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        maintenanceTab.Controls.AddRange(new Control[]
        {
            headingLabel,
            explanationLabel,
            autostartHeadingLabel,
            autostartCheckBox,
            autostartHintLabel,
            foldersHeadingLabel,
            openDataFolderButton,
            openProgramFolderButton,
            diagnosticsHeadingLabel,
            diagnosticsStatusLabel,
            openDiagnosticsLogButton,
            activeExtensionsButton,
            cleanupHeadingLabel,
            registryPathLabel,
            removeRegistrySettingsButton,
            cleanupHintLabel
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

        var settings = new AppSettings
        {
            DataFolderPath = dataFolder,
            IgnoreTempFolders = _ignoreTempFoldersCheckBox.Checked,
            ExtensionsToMonitor = extensions
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

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About DeskPulse";
        Width = 430;
        Height = 210;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var title = new Label
        {
            Text = $"{AppInfo.AppName} {AppInfo.Version}",
            Left = 20,
            Top = 20,
            Width = 360,
            Font = new System.Drawing.Font(Font.FontFamily, 12, System.Drawing.FontStyle.Bold)
        };

        var description = new Label
        {
            Text = "Windows tray activity monitor using ETW file I/O tracing, SQLite storage, and XLSX export.",
            Left = 20,
            Top = 55,
            Width = 370,
            Height = 40
        };

        var link = new LinkLabel
        {
            Text = AppInfo.GitHubUrl,
            Left = 20,
            Top = 102,
            Width = 370
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

        var okButton = new Button
        {
            Text = "OK",
            Left = 310,
            Top = 130,
            Width = 80,
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[]
        {
            title,
            description,
            link,
            okButton
        });

        AcceptButton = okButton;
    }
}