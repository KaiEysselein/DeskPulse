using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

namespace DeskPulse;

public static class AppInfo
{
    public const string AppName = "DeskPulse";
    public const string Version = "0.2.0.0";
    public const string GitHubUrl = "https://github.com/KaiEysselein/DeskPulse";
    public const string PipeName = "DeskPulse.Service.0.2";
}

public sealed class FileIoMonitor : IDisposable
{
    private const string SessionName = "DeskPulseSession";

    private readonly object _openFilesLock = new();
    private readonly object _settingsLock = new();
    private readonly object _monitoringStateLock = new();
    private readonly Dictionary<string, List<FileOpenInfo>> _openEventsByFile = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;
    private DeskPulseDatabase _database;
    private ProgramActivityMonitor _programActivityMonitor;
    private TraceEventSession? _session;
    private Thread? _workerThread;
    private bool _sessionEventsSubscribed;
    private bool _disposed;
    private volatile bool _loggingPaused;

    public bool IsLoggingPaused => _loggingPaused;

    public FileIoMonitor(bool subscribeToInteractiveSessionEvents = false)
    {
        _settings = AppSettings.Load();

        EnsureDataFolderExists(_settings.DataFolderPath);

        _database = new DeskPulseDatabase(_settings.DatabaseFilePath);
        _database.Initialize();
        _programActivityMonitor = new ProgramActivityMonitor(GetSettingsSnapshot, GetDatabaseSnapshot);

        WriteDeskPulseProgramEvent("ServiceStarted", "DeskPulse service started", "DeskPulse background service startup completed");
        WriteUserEvent("DeskPulseServiceStarted", "DeskPulse service started", "DeskPulse background service startup detected");
        WriteWindowsStartupEventOnce();
        if (subscribeToInteractiveSessionEvents)
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

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => HandleSessionSwitch(e.Reason);

    public void HandleSessionSwitch(SessionSwitchReason reason)
    {
        switch (reason)
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
                WriteUserEvent("SessionLogoff", "User logged off", "Windows session logoff detected");
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

    private void WriteWindowsStartupEventOnce()
    {
        try
        {
            var bootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
            var bootKey = bootTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\DeskPulse");
            var lastRecordedBoot = key?.GetValue("LastRecordedWindowsBootUtc") as string;
            if (string.Equals(lastRecordedBoot, bootKey, StringComparison.Ordinal))
                return;

            WriteUserEvent(
                "WindowsStarted",
                "Windows started",
                $"Windows boot detected at approximately {bootTime:yyyy-MM-dd HH:mm:ss}");

            key?.SetValue("LastRecordedWindowsBootUtc", bootKey, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            TryWriteStartupDiagnostic(ex);
        }
    }

    public void WriteDeskPulseProgramEvent(string eventType, string eventDescription, string note)
    {
        if (_loggingPaused)
            return;

        try
        {
            var settings = GetSettingsSnapshot();
            var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DeskPulse.Service.exe");

            if (!settings.LogProgramActivity ||
                !LoggingRulesEngine.IsProgramActivityMonitored(executablePath, AppInfo.AppName, settings.AppActivityRules))
                return;

            GetDatabaseSnapshot().InsertProgramEvent(new ProgramEventRecord
            {
                EventTime = DateTime.Now,
                EventDescription = eventDescription,
                ProgramName = AppInfo.AppName,
                ProcessId = Environment.ProcessId,
                FilePath = executablePath,
                WindowTitle = "",
                UserName = Environment.UserName,
                MachineName = Environment.MachineName,
                AppVersion = AppInfo.Version,
                Note = note
            });
        }
        catch
        {
            // Application activity logging must never prevent DeskPulse startup or shutdown.
        }
    }

    public void WriteUserEvent(string eventType, string eventDescription, string note)
    {
        if (_loggingPaused)
            return;

        try
        {
            var settings = GetSettingsSnapshot();
            if (!LoggingRulesEngine.IsUserActivityMonitored(eventType, eventDescription, settings.UserActivityRules))
                return;

            GetDatabaseSnapshot().InsertUserEvent(new UserEventRecord
            {
                EventTime = DateTime.Now,
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

        lock (_monitoringStateLock)
        {
            if (_disposed || _loggingPaused)
                return;

            StartLoggingComponents();
        }
    }

    public void PauseLogging()
    {
        lock (_monitoringStateLock)
        {
            if (_disposed || _loggingPaused)
                return;

            _loggingPaused = true;

            try { _session?.Dispose(); } catch { }
            _session = null;

            try { _programActivityMonitor.Pause(); } catch { }

            lock (_openFilesLock)
            {
                _openEventsByFile.Clear();
            }
        }

        try
        {
            if (_workerThread != null && _workerThread.IsAlive)
                _workerThread.Join(TimeSpan.FromSeconds(3));
        }
        catch { }
    }

    public void ResumeLogging()
    {
        lock (_monitoringStateLock)
        {
            if (_disposed || !_loggingPaused)
                return;

            if (TraceEventSession.IsElevated() != true)
                throw new InvalidOperationException("Windows kernel ETW file I/O tracing requires elevation.");

            _loggingPaused = false;
            StartLoggingComponents();
        }
    }

    private void StartLoggingComponents()
    {
        if (_workerThread == null || !_workerThread.IsAlive)
        {
            _workerThread = new Thread(RunEtwSession)
            {
                IsBackground = true,
                Name = "DeskPulse ETW Monitor"
            };
            _workerThread.Start();
        }

        _programActivityMonitor.Resume();
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
            if (!_loggingPaused && !_disposed)
                TryWriteError(ex);
        }
        finally
        {
            _session = null;
        }
    }

    private void HandleFileEvent(string operation, TraceEvent data)
    {
        if (_loggingPaused || _disposed)
            return;

        var processId = data.ProcessID;
        var processName = string.IsNullOrWhiteSpace(data.ProcessName)
            ? "UnknownProcess"
            : data.ProcessName;

        if (IsDeskPulseProcess(processId, processName))
            return;

        var rawFileName = TryGetPayloadString(data, "FileName");

        if (string.IsNullOrWhiteSpace(rawFileName))
        {
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
        if (_loggingPaused || _disposed)
            return;

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
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            if (settings.IgnoreTempFolders && PathExclusions.IsInTempFolder(fullPath))
            {
                return false;
            }

            // Explicit App Activity rules take precedence for executable files. This allows a
            // specifically included app (for example abc.exe) to remain monitored even when
            // the general *.exe pattern is not included in File Activity rules.
            var appRuleDecision = string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)
                ? LoggingRulesEngine.GetProgramActivityRuleDecision(
                    fullPath,
                    Path.GetFileName(fullPath),
                    settings.AppActivityRules)
                : null;

            if (appRuleDecision == false)
                return false;

            var appIncludeOverridesFileRules = appRuleDecision == true;

            if (!appIncludeOverridesFileRules &&
                !LoggingRulesEngine.IsFileActivityMonitored(fullPath, settings.FileActivityRules))
                return false;

            var dbPath = Path.GetFullPath(settings.DatabaseFilePath);
            var exportPath = Path.GetFullPath(settings.ExcelExportFilePath);

            if (string.Equals(fullPath, dbPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(fullPath, exportPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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
            WriteUserEvent("DeskPulseServiceStopped", "DeskPulse service stopped", "DeskPulse background service shutdown started");
            WriteDeskPulseProgramEvent("ServiceStopped", "DeskPulse service stopped", "DeskPulse background service shutdown started");
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
    private bool _paused;

    public ProgramActivityMonitor(Func<AppSettings> getSettings, Func<DeskPulseDatabase> getDatabase)
    {
        _getSettings = getSettings;
        _getDatabase = getDatabase;
    }

    public void Start() => Resume();

    public void Pause()
    {
        lock (_lock)
        {
            if (_disposed || _paused)
                return;

            _paused = true;
            _timer?.Dispose();
            _timer = null;
            _knownProcesses.Clear();
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _paused = false;

            if (_timer != null || !_getSettings().LogProgramActivity)
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

            if (_paused)
            {
                _timer?.Dispose();
                _timer = null;
                _knownProcesses.Clear();
                return;
            }

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
        if (_disposed || _paused)
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
                if (LoggingRulesEngine.IsProgramActivityMonitored(process.FilePath, process.ProcessName, settings.AppActivityRules))
                    WriteProgramEvent("ProgramStarted", "Program started", process, process.StartTime ?? DateTime.Now, "Detected in the current interactive Windows session");
            }

            foreach (var process in stoppedProcesses)
            {
                if (LoggingRulesEngine.IsProgramActivityMonitored(process.FilePath, process.ProcessName, settings.AppActivityRules))
                    WriteProgramEvent("ProgramStopped", "Program closed", process, DateTime.Now, "Process no longer detected in the current interactive Windows session");
            }
        }
        catch (Exception ex)
        {
            _ = ex; // Program scan errors are ignored so monitoring can continue.
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
            _ = ex; // Program event write errors are ignored so monitoring can continue.
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

            ExecuteNonQuery(connection, "DROP INDEX IF EXISTS IX_UserEvents_EventType;");
            ExecuteNonQuery(connection, "DROP INDEX IF EXISTS IX_ProgramEvents_EventType;");
            RemoveColumnIfExists(connection, "UserEvents", "EventType");
            RemoveColumnIfExists(connection, "ProgramEvents", "EventType");

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_UserEvents_CreatedAt ON UserEvents (CreatedAt);"
            );

            ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS IX_ProgramEvents_CreatedAt ON ProgramEvents (CreatedAt);"
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
                        COALESCE(NULLIF(EventDescription, ''), '(mixed)') AS Extra,
                        COUNT(*) AS EventCount,
                        MIN(CreatedAt) AS FirstSeen,
                        MAX(CreatedAt) AS LastSeen
                    FROM ProgramEvents
                    GROUP BY COALESCE(NULLIF(ProgramName, ''), '(blank)'), COALESCE(NULLIF(EventDescription, ''), '(mixed)')
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


    public MaintenanceExclusionCleanupResult CleanDatabaseWithCurrentRules(
        AppSettings settings,
        IProgress<ExportProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            ExecuteNonQuery(connection, "PRAGMA busy_timeout=5000;");

            progress?.Report(new ExportProgressInfo(1, "1%   Reading database history"));

            var activityIdsToDelete = new List<long>();
            var userIdsToDelete = new List<long>();
            var programIdsToDelete = new List<long>();

            long totalRecords = CountTableRows(connection, "ActivityEvents") +
                                CountTableRows(connection, "UserEvents") +
                                CountTableRows(connection, "ProgramEvents");
            long scanned = 0;

            void ReportScanProgress()
            {
                scanned++;
                if (scanned % 250 != 0 && scanned != totalRecords)
                    return;

                var percent = totalRecords == 0 ? 35 : 1 + (int)Math.Round((double)scanned / totalRecords * 34D);
                progress?.Report(new ExportProgressInfo(Math.Min(35, percent),
                    Math.Min(35, percent).ToString(CultureInfo.InvariantCulture) + "%   Evaluating historical records (" +
                    scanned.ToString("N0", CultureInfo.InvariantCulture) + " of " + totalRecords.ToString("N0", CultureInfo.InvariantCulture) + ")"));
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, COALESCE(FullPath, ''), COALESCE(ProcessName, ''), COALESCE(Extension, '') FROM ActivityEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = reader.GetInt64(0);
                    var fullPath = reader.GetString(1);
                    var processName = reader.GetString(2);
                    var extension = reader.GetString(3);
                    var keep = ShouldKeepHistoricalFileRecord(fullPath, processName, extension, settings);
                    if (!keep)
                        activityIdsToDelete.Add(id);
                    ReportScanProgress();
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, COALESCE(EventDescription, '') FROM UserEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = reader.GetInt64(0);
                    var eventDescription = reader.GetString(1);
                    if (!LoggingRulesEngine.IsUserActivityMonitored("", eventDescription, settings.UserActivityRules))
                        userIdsToDelete.Add(id);
                    ReportScanProgress();
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, COALESCE(FilePath, ''), COALESCE(ProgramName, '') FROM ProgramEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = reader.GetInt64(0);
                    var filePath = reader.GetString(1);
                    var programName = reader.GetString(2);
                    if (!settings.LogProgramActivity || !LoggingRulesEngine.IsProgramActivityMonitored(filePath, programName, settings.AppActivityRules))
                        programIdsToDelete.Add(id);
                    ReportScanProgress();
                }
            }

            var totalToDelete = activityIdsToDelete.Count + userIdsToDelete.Count + programIdsToDelete.Count;
            if (totalToDelete == 0)
            {
                progress?.Report(new ExportProgressInfo(92, "92%  No conflicting records found; compacting database"));
                ExecuteNonQuery(connection, "VACUUM;");
                progress?.Report(new ExportProgressInfo(100, "100% Database housekeeping complete"));
                return new MaintenanceExclusionCleanupResult();
            }

            var deleted = 0;
            void ReportDeleteProgress(int count, string description)
            {
                deleted += count;
                var percent = 35 + (int)Math.Round((double)deleted / Math.Max(1, totalToDelete) * 55D);
                percent = Math.Max(36, Math.Min(90, percent));
                progress?.Report(new ExportProgressInfo(percent,
                    percent.ToString(CultureInfo.InvariantCulture) + "%   Removing " + description + " (" +
                    deleted.ToString("N0", CultureInfo.InvariantCulture) + " of " + totalToDelete.ToString("N0", CultureInfo.InvariantCulture) + ")"));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    DeleteRowsByIds(connection, transaction, "ActivityEvents", activityIdsToDelete, cancellationToken, count => ReportDeleteProgress(count, "file activity records"));
                    DeleteRowsByIds(connection, transaction, "UserEvents", userIdsToDelete, cancellationToken, count => ReportDeleteProgress(count, "user activity records"));
                    DeleteRowsByIds(connection, transaction, "ProgramEvents", programIdsToDelete, cancellationToken, count => ReportDeleteProgress(count, "app activity records"));
                    cancellationToken.ThrowIfCancellationRequested();
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            progress?.Report(new ExportProgressInfo(92, "92%  Deletions committed; compacting SQLite database (cannot be cancelled)"));
            ExecuteNonQuery(connection, "VACUUM;");
            progress?.Report(new ExportProgressInfo(100, "100% Database housekeeping complete"));

            return new MaintenanceExclusionCleanupResult
            {
                ActivityRecordsDeleted = activityIdsToDelete.Count,
                UserRecordsDeleted = userIdsToDelete.Count,
                ProgramRecordsDeleted = programIdsToDelete.Count
            };
        }
    }

    private static bool ShouldKeepHistoricalFileRecord(string fullPath, string processName, string extension, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        if (settings.IgnoreTempFolders && PathExclusions.IsInTempFolder(fullPath))
            return false;

        if (string.IsNullOrWhiteSpace(extension))
            extension = PathUtilities.GetExtension(fullPath);

        var appDecision = string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            ? LoggingRulesEngine.GetProgramActivityRuleDecision(fullPath, Path.GetFileName(fullPath), settings.AppActivityRules)
            : null;

        if (appDecision == false)
            return false;

        var appIncludeOverrides = appDecision == true;
        if (!appIncludeOverrides && !LoggingRulesEngine.IsFileActivityMonitored(fullPath, settings.FileActivityRules))
            return false;


        try
        {
            if (string.Equals(Path.GetFullPath(fullPath), Path.GetFullPath(settings.DatabaseFilePath), StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(Path.GetFullPath(fullPath), Path.GetFullPath(settings.ExcelExportFilePath), StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch
        {
            // Keep evaluating manually entered or historical paths that can no longer be normalised.
        }

        return true;
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

    private static void DeleteRowsByIds(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken,
        Action<int>? progressCallback = null)
    {
        if (ids.Count == 0)
            return;

        foreach (var chunk in ids.Chunk(500))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

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
                EventDescription = ReadText(reader, 4),
                UserName = ReadText(reader, 5),
                MachineName = ReadText(reader, 6),
                ProcessName = ReadText(reader, 7),
                ProcessId = ReadNullableLong(reader, 8),
                AppVersion = ReadText(reader, 9),
                Note = ReadText(reader, 10)
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
                EventDescription = ReadText(reader, 4),
                ProgramName = ReadText(reader, 5),
                ProcessId = ReadNullableLong(reader, 6),
                FilePath = ReadText(reader, 7),
                WindowTitle = ReadText(reader, 8),
                UserName = ReadText(reader, 9),
                MachineName = ReadText(reader, 10),
                AppVersion = ReadText(reader, 11),
                Note = ReadText(reader, 12)
            });
        }

        return result;
    }

    private static void RemoveColumnIfExists(SqliteConnection connection, string tableName, string columnName)
    {
        if (!ColumnExists(connection, tableName, columnName))
            return;

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} DROP COLUMN {columnName};");
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
    public long UserRecordsDeleted { get; set; }
    public long TotalRecordsDeleted => ActivityRecordsDeleted + ProgramRecordsDeleted + UserRecordsDeleted;
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

        if (ruleType.Equals("event", StringComparison.OrdinalIgnoreCase))
            return BuildEventRule(value, isInclude);

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

            if (parts[1].Equals("event", StringComparison.OrdinalIgnoreCase))
                return ParseEventRule(value);
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

    public static string BuildEventRule(string eventName, bool isInclude)
    {
        eventName = (eventName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(eventName))
            return "";
        return (isInclude ? "include" : "exclude") + "|event||" + eventName;
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

    public static ExclusionRule ParseEventRule(string value)
    {
        value = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return new ExclusionRule { IsInclude = false, RuleType = "event", IncludeSubfolders = false, Value = "" };
        var parts = value.Split('|', 4);
        if (parts.Length == 4 &&
            (parts[0].Equals("include", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("exclude", StringComparison.OrdinalIgnoreCase)) &&
            parts[1].Equals("event", StringComparison.OrdinalIgnoreCase))
        {
            return new ExclusionRule
            {
                IsInclude = parts[0].Equals("include", StringComparison.OrdinalIgnoreCase),
                RuleType = "event", IncludeSubfolders = false, Value = parts[3].Trim()
            };
        }
        return new ExclusionRule { IsInclude = false, RuleType = "event", IncludeSubfolders = false, Value = value };
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
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(rule.Value))
            return false;

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fileName = Path.GetFileName(normalizedPath);
            var ruleValue = Environment.ExpandEnvironmentVariables(rule.Value.Trim());
            var hasPath = ruleValue.Contains(Path.DirectorySeparatorChar) ||
                          ruleValue.Contains(Path.AltDirectorySeparatorChar) ||
                          Path.IsPathRooted(ruleValue);
            var candidate = hasPath ? normalizedPath : fileName;

            if (ruleValue.Contains('*') || ruleValue.Contains('?'))
            {
                var wildcardPattern = hasPath
                    ? ruleValue.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    : ruleValue;
                var regexPattern = "^" + Regex.Escape(wildcardPattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";

                return Regex.IsMatch(candidate, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            if (!hasPath)
                return fileName.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);

            var normalizedRule = Path.GetFullPath(ruleValue)
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
    public static bool IsFileActivityMonitored(string fullPath, IEnumerable<string> fileRules)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return false;

        foreach (var ruleText in fileRules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);
            if (!rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rule.Value))
                continue;

            if (FileRuleMatches(fullPath, rule))
                return !rule.IsExclude;
        }

        return false;
    }

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

    public static bool IsUserActivityMonitored(string eventType, string eventDescription, IEnumerable<string> loggingRules)
    {
        return GetUserActivityRuleDecision(eventType, eventDescription, loggingRules) == true;
    }

    /// <summary>
    /// Returns true for an explicit Include match, false for an explicit Exclude match,
    /// and null when no User Activity rule matches. Rules are evaluated top-to-bottom.
    /// </summary>
    public static bool? GetUserActivityRuleDecision(string eventType, string eventDescription, IEnumerable<string> loggingRules)
    {
        foreach (var ruleText in loggingRules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);
            if (!rule.RuleType.Equals("event", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rule.Value))
                continue;

            var value = rule.Value.Trim();
            var matches = ContainsWildcard(value)
                ? WildcardMatches(eventType ?? "", value) || WildcardMatches(eventDescription ?? "", value)
                : string.Equals(eventType, value, StringComparison.OrdinalIgnoreCase) ||
                  (eventDescription ?? "").Contains(value, StringComparison.OrdinalIgnoreCase);

            if (matches)
                return !rule.IsExclude;
        }

        return null;
    }

    public static bool IsProgramActivityMonitored(string filePath, string processName, IEnumerable<string> loggingRules)
    {
        return GetProgramActivityRuleDecision(filePath, processName, loggingRules) == true;
    }

    /// <summary>
    /// Returns true for an explicit Include match, false for an explicit Exclude match,
    /// and null when no App Activity rule matches. Rules are evaluated top-to-bottom.
    /// </summary>
    public static bool? GetProgramActivityRuleDecision(string filePath, string processName, IEnumerable<string> loggingRules)
    {
        foreach (var ruleText in loggingRules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);

            if (string.IsNullOrWhiteSpace(rule.Value))
                continue;

            var matches = rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase)
                ? AppRuleMatches(filePath, processName, rule)
                : rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase)
                    ? FileRuleMatches(filePath, rule)
                    : FolderRuleMatches(filePath, rule);

            if (matches)
                return !rule.IsExclude;
        }

        return null;
    }

    private static bool AppRuleMatches(string filePath, string processName, ExclusionRule rule)
    {
        var ruleValue = (rule.Value ?? "").Trim().Trim('"');

        if (ruleValue.Length == 0)
            return false;

        if (Path.IsPathRooted(ruleValue) && !ContainsWildcard(ruleValue))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) &&
                    Path.GetFullPath(filePath).Equals(Path.GetFullPath(ruleValue), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // Fall back to executable-name matching below.
            }
        }

        return ProcessRuleMatches(processName, rule);
    }

    private static bool ProcessRuleMatches(string processName, ExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var ruleValue = (rule.Value ?? "").Trim().Trim('"');
        var ruleProcessName = Path.GetFileName(ruleValue);

        if (string.IsNullOrWhiteSpace(ruleProcessName))
            ruleProcessName = ruleValue;

        var normalizedProcessName = NormalizeProcessName(processName);
        var normalizedRuleName = NormalizeProcessName(ruleProcessName);

        if (ContainsWildcard(normalizedRuleName))
            return WildcardMatches(normalizedProcessName, normalizedRuleName);

        return normalizedProcessName.Equals(normalizedRuleName, StringComparison.OrdinalIgnoreCase);
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
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(rule.Value))
            return false;

        var expandedRule = Environment.ExpandEnvironmentVariables(rule.Value.Trim());
        var fileName = Path.GetFileName(fullPath) ?? "";

        if (expandedRule.IndexOfAny(new[] { '*', '?' }) >= 0)
        {
            var target = expandedRule.Contains(Path.DirectorySeparatorChar) || expandedRule.Contains(Path.AltDirectorySeparatorChar)
                ? fullPath
                : fileName;
            return FileWildcardMatches(target, expandedRule);
        }

        if (!Path.IsPathRooted(expandedRule) && expandedRule.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0)
            return fileName.Equals(expandedRule, StringComparison.OrdinalIgnoreCase);

        try
        {
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(fullPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedRule = Path.GetFullPath(expandedRule)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedPath.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool FileWildcardMatches(string value, string pattern)
    {
        var normalizedValue = (value ?? "").Replace('/', '\\');
        var normalizedPattern = NormalizeWindowsGlobPattern((pattern ?? "").Replace('/', '\\'));
        var regexPattern = BuildWindowsGlobRegex(normalizedPattern);

        return System.Text.RegularExpressions.Regex.IsMatch(
            normalizedValue,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string NormalizeWindowsGlobPattern(string pattern)
    {
        // Windows treats a path segment of *.* as all filenames, including names without
        // an extension. Only complete path segments are normalised so patterns such as
        // report*.* keep their intended meaning.
        return string.Join("\\", pattern.Split('\\').Select(segment => segment == "*.*" ? "*" : segment));
    }

    private static string BuildWindowsGlobRegex(string pattern)
    {
        var builder = new System.Text.StringBuilder("^");

        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];

            // A recursive folder token (\**\) matches zero or more complete folder levels.
            // This means C:\Work\**\* matches files directly in C:\Work as well as files
            // in any descendant folder.
            if (current == '\\' && index + 3 < pattern.Length &&
                pattern[index + 1] == '*' && pattern[index + 2] == '*' && pattern[index + 3] == '\\')
            {
                builder.Append(@"\\(?:[^\\]+\\)*");
                index += 3;
                continue;
            }

            if (current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                builder.Append(".*");
                index++;
                continue;
            }

            if (current == '*')
            {
                builder.Append(@"[^\\]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append(@"[^\\]");
                continue;
            }

            builder.Append(System.Text.RegularExpressions.Regex.Escape(current.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }

    private static bool ContainsWildcard(string value)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOfAny(new[] { '*', '?' }) >= 0;
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            value ?? "",
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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
        var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DeskPulse.Service.exe");

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

public sealed class ActivityRuleSetting
{
    public bool Enabled { get; set; } = true;
    public string RuleType { get; set; } = "";
    public string Action { get; set; } = "Exclude";
    public string Value { get; set; } = "";
    public bool IncludeSubfolders { get; set; }

    public string ToRuleText()
    {
        var isInclude = Action.Equals("Include", StringComparison.OrdinalIgnoreCase);
        return ExclusionRuleParser.BuildRule(RuleType, Value, isInclude, IncludeSubfolders);
    }

    public ActivityRuleSetting Clone() => new()
    {
        Enabled = Enabled,
        RuleType = RuleType,
        Action = Action,
        Value = Value,
        IncludeSubfolders = IncludeSubfolders
    };

    public static ActivityRuleSetting? FromRuleText(string text)
    {
        var rule = ExclusionRuleParser.ParseRule(text);
        if (string.IsNullOrWhiteSpace(rule.Value))
            return null;

        return new ActivityRuleSetting
        {
            Enabled = true,
            RuleType = rule.RuleType,
            Action = rule.IsInclude ? "Include" : "Exclude",
            Value = rule.Value,
            IncludeSubfolders = rule.IncludeSubfolders
        };
    }
}

public sealed class AppSettings
{
    private const string RegistryPath = @"Software\DeskPulse";
    private const int CurrentSettingsSchemaVersion = 5;

    public string DataFolderPath { get; set; } = GetDefaultDataFolderPath();

    public bool IgnoreTempFolders { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool LogProgramActivity { get; set; } = true;

    // Legacy combined list retained for migration compatibility.
    public List<string> LoggingRules { get; set; } = GetDefaultLoggingRules();

    public List<ActivityRuleSetting> FileActivityRuleSettings { get; set; } = ToRuleSettings(GetDefaultFileActivityRules());
    public List<ActivityRuleSetting> FolderActivityRuleSettings { get; set; } = ToRuleSettings(GetDefaultFolderActivityRules());
    public List<ActivityRuleSetting> UserActivityRuleSettings { get; set; } = ToRuleSettings(GetDefaultUserActivityRules());
    public List<ActivityRuleSetting> AppActivityRuleSettings { get; set; } = ToRuleSettings(GetDefaultAppActivityRules());

    public List<string> FileActivityRules
    {
        get => ActiveRuleTexts(FileActivityRuleSettings);
        set => FileActivityRuleSettings = ToRuleSettings(value);
    }

    public List<string> FolderActivityRules
    {
        get => ActiveRuleTexts(FolderActivityRuleSettings);
        set => FolderActivityRuleSettings = ToRuleSettings(value);
    }

    public List<string> UserActivityRules
    {
        get => ActiveRuleTexts(UserActivityRuleSettings);
        set => UserActivityRuleSettings = ToRuleSettings(value);
    }

    public List<string> AppActivityRules
    {
        get => ActiveRuleTexts(AppActivityRuleSettings);
        set => AppActivityRuleSettings = ToRuleSettings(value);
    }

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
            FileActivityRuleSettings = FileActivityRuleSettings.Select(rule => rule.Clone()).ToList(),
            FolderActivityRuleSettings = FolderActivityRuleSettings.Select(rule => rule.Clone()).ToList(),
            UserActivityRuleSettings = UserActivityRuleSettings.Select(rule => rule.Clone()).ToList(),
            AppActivityRuleSettings = AppActivityRuleSettings.Select(rule => rule.Clone()).ToList(),
            ExcludedFolders = new List<string>(ExcludedFolders),
            ExcludedProcesses = new HashSet<string>(ExcludedProcesses, StringComparer.OrdinalIgnoreCase),
            ExtensionsToMonitor = new HashSet<string>(ExtensionsToMonitor, StringComparer.OrdinalIgnoreCase),
            ExportSheets = ExportSheetOption.NormalizeList(ExportSheets)
        };
    }

    private static readonly string SharedSettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DeskPulse");
    private static readonly string SharedSettingsFile = Path.Combine(SharedSettingsFolder, "settings.json");
    private static readonly object SettingsFileLock = new();

    public static AppSettings Load()
    {
        lock (SettingsFileLock)
        {
            try
            {
                if (File.Exists(SharedSettingsFile))
                {
                    var json = File.ReadAllText(SharedSettingsFile);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (loaded != null)
                    {
                        NormalizeSharedSettings(loaded);
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall through to legacy migration/defaults.
            }

            var migrated = LoadLegacyRegistry();
            NormalizeSharedSettings(migrated);
            migrated.Save();
            return migrated;
        }
    }

    private static void NormalizeSharedSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DataFolderPath))
            settings.DataFolderPath = GetDefaultDataFolderPath();
        settings.LoggingRules ??= GetDefaultLoggingRules();
        settings.FileActivityRuleSettings ??= ToRuleSettings(GetDefaultFileActivityRules());
        settings.FolderActivityRuleSettings ??= new List<ActivityRuleSetting>();
        settings.UserActivityRuleSettings ??= ToRuleSettings(GetDefaultUserActivityRules());
        settings.AppActivityRuleSettings ??= ToRuleSettings(GetDefaultAppActivityRules());
        settings.ExcludedFolders ??= GetDefaultExcludedFolders();
        settings.ExcludedProcesses ??= GetDefaultExcludedProcesses();
        settings.ExtensionsToMonitor ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        settings.ExportSheets = ExportSheetOption.NormalizeList(settings.ExportSheets);
        settings.FileActivityRuleSettings = MergeFolderRulesIntoFileRules(settings.FileActivityRuleSettings, settings.FolderActivityRuleSettings);
        settings.FolderActivityRuleSettings = new List<ActivityRuleSetting>();
        EnsureRequiredUserActivityRules(settings);
    }

    private static AppSettings LoadLegacyRegistry()
    {
        var settings = new AppSettings();

        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);

        if (key == null)
        {
            settings.Save();
            return settings;
        }

        var schemaVersion = ReadInt(key, "SettingsSchemaVersion", 0);
        if (schemaVersion >= 2)
        {
            LoadSchemaV2(settings, key);

            var addedRequiredUserActivityRules = EnsureRequiredUserActivityRules(settings);

            if (schemaVersion < CurrentSettingsSchemaVersion)
            {
                UpgradeRuleSchema(settings);
                settings.Save();
            }
            else if (addedRequiredUserActivityRules)
            {
                settings.Save();
            }

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

        settings.FileActivityRules = ParsePlainListPreserveOrder(ReadString(key, "FileActivityRules", ""));
        settings.FolderActivityRules = ParsePlainListPreserveOrder(ReadString(key, "FolderActivityRules", ""));
        settings.UserActivityRules = ParsePlainListPreserveOrder(ReadString(key, "UserActivityRules", ""));
        settings.AppActivityRules = ParsePlainListPreserveOrder(ReadString(key, "AppActivityRules", ""));

        RedistributeActivityRules(settings);

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

        settings.FileActivityRules = settings.FileActivityRules
            .Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase) && rule.IsInclude && !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildFileRule(rule.Value, true))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (settings.FileActivityRules.Count == 0)
        {
            settings.FileActivityRules = settings.ExtensionsToMonitor
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .Select(extension => ExclusionRuleParser.BuildFileRule("*" + extension, true))
                .ToList();
        }

        if (settings.ExcludedFolders.Count == 0)
            settings.ExcludedFolders = GetDefaultExcludedFolders();

        if (settings.ExcludedProcesses.Count == 0)
            settings.ExcludedProcesses = GetDefaultExcludedProcesses();

        if (settings.LoggingRules.Count == 0)
            settings.LoggingRules = GetDefaultLoggingRules();
        if (settings.FolderActivityRules.Count == 0)
            settings.FolderActivityRules = GetDefaultFolderActivityRules();
        if (settings.AppActivityRules.Count == 0)
            settings.AppActivityRules = GetDefaultAppActivityRules();

        EnsureRequiredUserActivityRules(settings);
        settings.Save();

        return settings;
    }

    public void Save()
    {
        lock (SettingsFileLock)
        {
            NormalizeSharedSettings(this);
            Directory.CreateDirectory(SharedSettingsFolder);
            var temporaryFile = SharedSettingsFile + ".tmp";
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryFile, json);
            File.Move(temporaryFile, SharedSettingsFile, true);
        }
    }

    private static void LoadSchemaV2(AppSettings settings, RegistryKey rootKey)
    {
        using (var generalKey = rootKey.OpenSubKey("General"))
        {
            settings.DataFolderPath = generalKey == null ? GetDefaultDataFolderPath() : ReadString(generalKey, "DataFolderPath", GetDefaultDataFolderPath());
            settings.IgnoreTempFolders = generalKey == null || ReadBool(generalKey, "IgnoreTempFolders", true);
            settings.StartWithWindows = generalKey != null && ReadBool(generalKey, "StartWithWindows", StartupTaskManager.IsEnabled());
            settings.LogProgramActivity = generalKey == null || ReadBool(generalKey, "LogProgramActivity", true);
        }

        using (var rulesKey = rootKey.OpenSubKey("Rules"))
        {
            settings.FileActivityRuleSettings = ReadRuleSettingsJson(rulesKey, "FileActivity", ToRuleSettings(GetDefaultFileActivityRules()));
            settings.FolderActivityRuleSettings = ReadRuleSettingsJson(rulesKey, "FolderActivity", ToRuleSettings(GetDefaultFolderActivityRules()));
            settings.UserActivityRuleSettings = ReadRuleSettingsJson(rulesKey, "UserActivity", ToRuleSettings(GetDefaultUserActivityRules()));
            settings.AppActivityRuleSettings = ReadRuleSettingsJson(rulesKey, "AppActivity", ToRuleSettings(GetDefaultAppActivityRules()));
            settings.FileActivityRuleSettings = MergeFolderRulesIntoFileRules(
                settings.FileActivityRuleSettings, settings.FolderActivityRuleSettings);
            settings.FolderActivityRuleSettings = new List<ActivityRuleSetting>();
        }

        using (var exportKey = rootKey.OpenSubKey("Export"))
        {
            var exportText = exportKey == null
                ? ExportSheetOption.ToRegistryText(settings.ExportSheets)
                : ReadString(exportKey, "Sheets", ExportSheetOption.ToRegistryText(settings.ExportSheets));
            settings.ExportSheets = ExportSheetOption.ParseList(exportText);
        }

        settings.ExcludedFolders = new List<string>();
        settings.ExcludedProcesses = new HashSet<string>(ExtractLegacyProcessValues(settings.AppActivityRules), StringComparer.OrdinalIgnoreCase);
        settings.LoggingRules = settings.FileActivityRules.Concat(settings.AppActivityRules).ToList();
        settings.ExtensionsToMonitor = ExtractExtensions(settings.FileActivityRuleSettings);
    }

    public static List<ActivityRuleSetting> MergeFolderRulesIntoFileRules(
        IEnumerable<ActivityRuleSetting>? fileRules,
        IEnumerable<ActivityRuleSetting>? folderRules)
    {
        var result = new List<ActivityRuleSetting>();

        // Folder rules are placed first so their more-specific path decisions take
        // precedence over broad file patterns such as *.xlsx.
        foreach (var folderRule in folderRules ?? Array.Empty<ActivityRuleSetting>())
        {
            if (string.IsNullOrWhiteSpace(folderRule.Value))
                continue;

            var folder = Environment.ExpandEnvironmentVariables(folderRule.Value.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (folder.Length == 0)
                continue;

            var suffix = folderRule.IncludeSubfolders ? @"\**\*" : @"\*";
            result.Add(new ActivityRuleSetting
            {
                Enabled = folderRule.Enabled,
                RuleType = "file",
                Action = folderRule.Action,
                Value = folder + suffix,
                IncludeSubfolders = false
            });
        }

        foreach (var fileRule in fileRules ?? Array.Empty<ActivityRuleSetting>())
        {
            if (string.IsNullOrWhiteSpace(fileRule.Value))
                continue;

            var clone = fileRule.Clone();
            clone.RuleType = "file";
            clone.IncludeSubfolders = false;
            result.Add(clone);
        }

        return result
            .GroupBy(rule => $"{rule.Enabled}|{rule.Action}|{rule.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void WriteRuleSettingsJson(RegistryKey? key, string valueName, IEnumerable<ActivityRuleSetting> rules)
    {
        if (key == null)
            return;
        key.SetValue(valueName, JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = false }), RegistryValueKind.String);
    }

    private static List<ActivityRuleSetting> ReadRuleSettingsJson(RegistryKey? key, string valueName, List<ActivityRuleSetting> fallback)
    {
        if (key == null)
            return fallback.Select(rule => rule.Clone()).ToList();

        var json = ReadString(key, valueName, "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<ActivityRuleSetting>();

        try
        {
            return (JsonSerializer.Deserialize<List<ActivityRuleSetting>>(json) ?? new List<ActivityRuleSetting>())
                .Where(rule => !string.IsNullOrWhiteSpace(rule.Value) && !string.IsNullOrWhiteSpace(rule.RuleType))
                .ToList();
        }
        catch
        {
            return fallback.Select(rule => rule.Clone()).ToList();
        }
    }

    private static List<ActivityRuleSetting> ToRuleSettings(IEnumerable<string> rules)
    {
        return (rules ?? Array.Empty<string>())
            .Select(ActivityRuleSetting.FromRuleText)
            .Where(rule => rule != null)
            .Select(rule => rule!)
            .ToList();
    }

    private static List<string> ActiveRuleTexts(IEnumerable<ActivityRuleSetting> rules)
    {
        return (rules ?? Array.Empty<ActivityRuleSetting>())
            .Where(rule => rule.Enabled)
            .Select(rule => rule.ToRuleText())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static HashSet<string> ExtractExtensions(IEnumerable<ActivityRuleSetting> rules)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules ?? Array.Empty<ActivityRuleSetting>())
        {
            var value = rule.Value.Trim();
            if (value.StartsWith("*.", StringComparison.Ordinal) && value.IndexOfAny(new[] { '?', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0)
                result.Add(value[1..].ToLowerInvariant());
        }
        return result;
    }

    private static List<string> ExtractLegacyFolderValues(IEnumerable<string> rules)
    {
        return rules.Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("folder", StringComparison.OrdinalIgnoreCase) && rule.IsExclude)
            .Select(rule => rule.Value).ToList();
    }

    private static IEnumerable<string> ExtractLegacyProcessValues(IEnumerable<string> rules)
    {
        return rules.Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase) && rule.IsExclude)
            .Select(rule => rule.Value);
    }

    private static void RedistributeActivityRules(AppSettings settings)
    {
        var allRules = settings.LoggingRules
            .Concat(settings.FileActivityRules)
            .Concat(settings.FolderActivityRules)
            .Concat(settings.UserActivityRules)
            .Concat(settings.AppActivityRules)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.FileActivityRules = RulesOfType(allRules, "file");
        settings.FolderActivityRules = RulesOfType(allRules, "folder");
        settings.UserActivityRules = RulesOfType(allRules, "event");
        settings.AppActivityRules = RulesOfType(allRules, "process");

        if (settings.FolderActivityRules.Count == 0)
            settings.FolderActivityRules = GetDefaultFolderActivityRules();
        if (settings.AppActivityRules.Count == 0)
            settings.AppActivityRules = GetDefaultAppActivityRules();
    }

    private static List<string> RulesOfType(IEnumerable<string> rules, string ruleType)
    {
        return rules
            .Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals(ruleType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildRule(rule.RuleType, rule.Value, rule.IsInclude, rule.IncludeSubfolders))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> GetDefaultFileActivityRules()
    {
        return new[] { ".txt", ".pdf", ".docx", ".xlsx", ".dwg", ".jpg", ".png", ".cs" }
            .Select(extension => ExclusionRuleParser.BuildFileRule("*" + extension, true))
            .ToList();
    }

    public static List<string> GetDefaultFolderActivityRules()
    {
        return GetDefaultExcludedFolders()
            .Select(folder => ExclusionRuleParser.ParseFolderRule(folder))
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildFolderRule(rule.Value, rule.IsInclude, rule.IncludeSubfolders))
            .ToList();
    }

    private static bool EnsureRequiredUserActivityRules(AppSettings settings)
    {
        var changed = false;
        var requiredEvents = new[]
        {
            "DeskPulseStarted",
            "DeskPulseStopped",
            "WindowsStarted",
            "SessionLocked",
            "SessionUnlocked",
            "SessionLogon",
            "SessionLogoff"
        };

        foreach (var eventType in requiredEvents)
        {
            var existing = settings.UserActivityRuleSettings.FirstOrDefault(setting =>
                setting.RuleType.Equals("event", StringComparison.OrdinalIgnoreCase) &&
                setting.Value.Trim().Equals(eventType, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (!existing.Enabled || !existing.Action.Equals("Include", StringComparison.OrdinalIgnoreCase))
                {
                    existing.Enabled = true;
                    existing.Action = "Include";
                    changed = true;
                }
                continue;
            }

            settings.UserActivityRuleSettings.Add(new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = "event",
                Action = "Include",
                Value = eventType,
                IncludeSubfolders = false
            });
            changed = true;
        }

        return changed;
    }

    public static List<string> GetDefaultUserActivityRules()
    {
        return new[]
        {
            "DeskPulseStarted",
            "DeskPulseStopped",
            "WindowsStarted",
            "SessionLocked",
            "SessionUnlocked",
            "SessionLogon",
            "SessionLogoff",
            "ConsoleConnect",
            "ConsoleDisconnect",
            "RemoteConnect",
            "RemoteDisconnect"
        }
        .Select(eventType => ExclusionRuleParser.BuildRule("event", eventType, true, false))
        .ToList();
    }

    public static List<string> GetDefaultAppActivityRules()
    {
        var rules = GetDefaultExcludedProcesses()
            .Select(process => ExclusionRuleParser.ParseProcessRule(process))
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildProcessRule(rule.Value, rule.IsInclude))
            .ToList();

        // DeskPulse lifecycle events are recorded in App Activity by default.
        rules.Add(ExclusionRuleParser.BuildProcessRule("DeskPulse", true));

        // Strict allow-list fallback: after the default noise exclusions, include all
        // other applications. Users can remove this rule to monitor only named apps.
        rules.Add(ExclusionRuleParser.BuildProcessRule("*", true));
        return rules;
    }

    private static void UpgradeRuleSchema(AppSettings settings)
    {
        // Schema 5 combines Folder Activity into File Activity. Existing folder
        // rules are converted to path wildcard patterns and the old list is cleared.
        settings.FileActivityRuleSettings = MergeFolderRulesIntoFileRules(
            settings.FileActivityRuleSettings, settings.FolderActivityRuleSettings);
        settings.FolderActivityRuleSettings = new List<ActivityRuleSetting>();

        // User Activity previously defaulted to logging unmatched events. Populate explicit
        // Include rules so schema 3 preserves the existing useful session/app lifecycle data.
        if (settings.UserActivityRuleSettings.Count == 0)
            settings.UserActivityRuleSettings = ToRuleSettings(GetDefaultUserActivityRules());

        // App Activity previously logged unmatched processes. Add a final Include-all rule
        // after existing ordered exclusions to preserve that behavior under strict matching.
        var hasCatchAllAppRule = settings.AppActivityRuleSettings.Any(setting =>
            setting.Enabled &&
            setting.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase) &&
            setting.Value.Trim().Equals("*", StringComparison.OrdinalIgnoreCase));

        var hasDeskPulseRule = settings.AppActivityRuleSettings.Any(setting =>
            setting.Enabled &&
            setting.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase) &&
            (setting.Value.Trim().Equals("DeskPulse", StringComparison.OrdinalIgnoreCase) ||
             setting.Value.Trim().Equals("DeskPulse.exe", StringComparison.OrdinalIgnoreCase)));

        if (!hasDeskPulseRule)
        {
            var catchAllIndex = settings.AppActivityRuleSettings.FindIndex(setting =>
                setting.Enabled &&
                setting.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase) &&
                setting.Value.Trim().Equals("*", StringComparison.OrdinalIgnoreCase));

            var deskPulseRule = new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = "process",
                Action = "Include",
                Value = "DeskPulse",
                IncludeSubfolders = false
            };

            if (catchAllIndex >= 0)
                settings.AppActivityRuleSettings.Insert(catchAllIndex, deskPulseRule);
            else
                settings.AppActivityRuleSettings.Add(deskPulseRule);
        }

        if (!hasCatchAllAppRule)
        {
            settings.AppActivityRuleSettings.Add(new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = "process",
                Action = "Include",
                Value = "*",
                IncludeSubfolders = false
            });
        }
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

    private static int ReadInt(RegistryKey key, string valueName, int defaultValue)
    {
        try
        {
            var value = key.GetValue(valueName);
            return value == null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
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

