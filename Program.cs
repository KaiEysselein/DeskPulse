using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

namespace DeskPulse;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayAppContext());
    }
}

public static class AppInfo
{
    public const string AppName = "DeskPulse";
    public const string AppVersion = "0.0.1";
    public const string GitHubUrl = "https://github.com/KaiEysselein/DeskPulse";

    public static string DisplayNameWithVersion => $"{AppName} v{AppVersion}";
}

public sealed class TrayAppContext : ApplicationContext
{
    private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "file-logger.ico");

    private readonly NotifyIcon _trayIcon;
    private readonly FileIoMonitor _monitor;
    private readonly System.Drawing.Icon _appIcon;

    public TrayAppContext()
    {
        _appIcon = LoadApplicationIcon();
        _monitor = new FileIoMonitor();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open log file in Excel", null, (_, _) => _monitor.OpenLogFileSnapshot());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettingsWindow());
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon,
            ContextMenuStrip = menu,
            Text = AppInfo.DisplayNameWithVersion,
            Visible = true,
            BalloonTipIcon = ToolTipIcon.Info
        };

        try
        {
            _monitor.Start();

            _trayIcon.BalloonTipTitle = AppInfo.AppName;
            _trayIcon.BalloonTipText = $"File activity monitoring started.\n\nLog file:\n{_monitor.CurrentLogFilePath}";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(3000);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{ex.Message}\n\nExpected log file path:\n{AppSettings.GetDefaultLogFilePath()}",
                $"{AppInfo.AppName} could not start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            ExitThread();
        }
    }

    private static System.Drawing.Icon LoadApplicationIcon()
    {
        try
        {
            if (File.Exists(IconPath))
            {
                return new System.Drawing.Icon(IconPath);
            }
        }
        catch
        {
            // Fall back to embedded executable icon.
        }

        try
        {
            var executablePath = Application.ExecutablePath;

            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                var extractedIcon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);

                if (extractedIcon != null)
                    return extractedIcon;
            }
        }
        catch
        {
            // Fall back to generic application icon.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void OpenSettingsWindow()
    {
        using var form = new SettingsForm();

        if (form.ShowDialog() == DialogResult.OK)
        {
            _monitor.ReloadSettings();

            _trayIcon.BalloonTipTitle = AppInfo.AppName;
            _trayIcon.BalloonTipText = $"Settings saved.\n\nLog file:\n{_monitor.CurrentLogFilePath}";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(3000);
        }
    }

    private static void ShowAbout()
    {
        using var aboutForm = new Form
        {
            Text = $"About {AppInfo.AppName}",
            Width = 520,
            Height = 300,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var titleLabel = new Label
        {
            Text = AppInfo.DisplayNameWithVersion,
            Left = 20,
            Top = 20,
            Width = 460,
            Height = 24,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
        };

        var descriptionLabel = new Label
        {
            Text = "Windows file activity monitor.\n\nLogs selected file open, write/save, and close activity to CSV.",
            Left = 20,
            Top = 58,
            Width = 460,
            Height = 70
        };

        var defaultLogLabel = new Label
        {
            Text = $"Default log file:\n{AppSettings.GetDefaultLogFilePath()}",
            Left = 20,
            Top = 135,
            Width = 460,
            Height = 44
        };

        var githubLabel = new LinkLabel
        {
            Text = $"GitHub: {AppInfo.GitHubUrl}",
            Left = 20,
            Top = 188,
            Width = 460,
            Height = 24
        };

        githubLabel.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppInfo.GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open GitHub link.\n\n{AppInfo.GitHubUrl}\n\nError:\n{ex.Message}",
                    AppInfo.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 395,
            Top = 222,
            Width = 90,
            DialogResult = DialogResult.OK
        };

        aboutForm.Controls.Add(titleLabel);
        aboutForm.Controls.Add(descriptionLabel);
        aboutForm.Controls.Add(defaultLogLabel);
        aboutForm.Controls.Add(githubLabel);
        aboutForm.Controls.Add(okButton);

        aboutForm.AcceptButton = okButton;
        aboutForm.CancelButton = okButton;

        aboutForm.ShowDialog();
    }

    protected override void ExitThreadCore()
    {
        _monitor.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _appIcon.Dispose();

        base.ExitThreadCore();
    }
}

public sealed class FileIoMonitor : IDisposable
{
    private const string SessionName = "DeskPulseSession";

    private readonly object _logLock = new();
    private readonly object _openFilesLock = new();
    private readonly object _settingsLock = new();

    private readonly Dictionary<string, List<FileOpenInfo>> _openEventsByFile = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;

    private TraceEventSession? _session;
    private Thread? _workerThread;
    private bool _disposed;

    public string CurrentLogFilePath
    {
        get
        {
            var settings = GetSettingsSnapshot();
            return settings.LogFilePath;
        }
    }

    public FileIoMonitor()
    {
        _settings = AppSettings.Load();
        EnsureLogFolderExists(_settings.LogFilePath);
        EnsureCsvHeaderExists();
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
            Name = "DeskPulse ETW File Monitor"
        };

        _workerThread.Start();
    }

    public void ReloadSettings()
    {
        lock (_settingsLock)
        {
            _settings = AppSettings.Load();
            EnsureLogFolderExists(_settings.LogFilePath);
            EnsureCsvHeaderExists();
        }
    }

    private AppSettings GetSettingsSnapshot()
    {
        lock (_settingsLock)
        {
            return _settings.Clone();
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

            session.Source.Kernel.FileIOCreate += data =>
            {
                HandleFileEvent("OPEN", data);
            };

            session.Source.Kernel.FileIOWrite += data =>
            {
                HandleFileEvent("WRITE", data);
            };

            session.Source.Kernel.FileIOClose += data =>
            {
                HandleFileEvent("CLOSE", data);
            };

            session.Source.Process();
        }
        catch (Exception ex)
        {
            WriteErrorToCsv(ex);
        }
    }

    private void HandleFileEvent(string operation, TraceEvent data)
    {
        var fileName = TryGetPayloadString(data, "FileName");

        if (string.IsNullOrWhiteSpace(fileName))
            return;

        if (!ShouldMonitorFile(fileName))
            return;

        var fullFileName = GetSafeFullPath(fileName);

        if (string.IsNullOrWhiteSpace(fullFileName))
            return;

        var processId = data.ProcessID;
        var processName = string.IsNullOrWhiteSpace(data.ProcessName)
            ? "UnknownProcess"
            : data.ProcessName;

        var key = BuildOpenFileKey(fullFileName, processId, processName);
        var eventTime = DateTime.Now;

        if (operation == "OPEN")
        {
            var openingSize = TryGetFileSize(fullFileName);

            RegisterOpenFileEvent(
                key,
                new FileOpenInfo
                {
                    OpenedTime = eventTime,
                    SizeAtOpening = openingSize
                }
            );

            return;
        }

        if (operation == "WRITE")
        {
            var writeSize = TryGetFileSize(fullFileName);
            var writeWasAttachedToOpenSession = RegisterFileWriteEvent(key, eventTime, writeSize);

            if (!writeWasAttachedToOpenSession)
            {
                WriteCsvRow(
                    file: fullFileName,
                    dateOpened: "",
                    timeOpened: "",
                    sizeAtOpening: "",
                    firstWriteDate: eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    firstWriteTime: eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    lastWriteDate: eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    lastWriteTime: eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    writeCount: "1",
                    sizeAtLastWrite: FormatFileSize(writeSize),
                    dateClosed: "",
                    timeClosed: "",
                    sizeAtClosing: "",
                    inferredAction: "Save/write detected",
                    process: processName,
                    processId: processId.ToString(CultureInfo.InvariantCulture),
                    note: "Write event seen, but matching open event was not found"
                );
            }

            return;
        }

        if (operation == "CLOSE")
        {
            var closingSize = TryGetFileSize(fullFileName);
            var openInfo = TryConsumeOpenFileEvent(key);

            if (openInfo == null)
            {
                WriteCsvRow(
                    file: fullFileName,
                    dateOpened: "",
                    timeOpened: "",
                    sizeAtOpening: "",
                    firstWriteDate: "",
                    firstWriteTime: "",
                    lastWriteDate: "",
                    lastWriteTime: "",
                    writeCount: "",
                    sizeAtLastWrite: "",
                    dateClosed: eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    timeClosed: eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    sizeAtClosing: FormatFileSize(closingSize),
                    inferredAction: "Unknown close action",
                    process: processName,
                    processId: processId.ToString(CultureInfo.InvariantCulture),
                    note: "Close event seen, but matching open event was not found"
                );

                return;
            }

            var inferredAction = InferFileAction(
                openInfo.SizeAtOpening,
                closingSize,
                openInfo.WriteCount
            );

            WriteCsvRow(
                file: fullFileName,
                dateOpened: openInfo.OpenedTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                timeOpened: openInfo.OpenedTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                sizeAtOpening: FormatFileSize(openInfo.SizeAtOpening),
                firstWriteDate: FormatDate(openInfo.FirstWriteTime),
                firstWriteTime: FormatTime(openInfo.FirstWriteTime),
                lastWriteDate: FormatDate(openInfo.LastWriteTime),
                lastWriteTime: FormatTime(openInfo.LastWriteTime),
                writeCount: openInfo.WriteCount > 0
                    ? openInfo.WriteCount.ToString(CultureInfo.InvariantCulture)
                    : "",
                sizeAtLastWrite: FormatFileSize(openInfo.SizeAtLastWrite),
                dateClosed: eventTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                timeClosed: eventTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                sizeAtClosing: FormatFileSize(closingSize),
                inferredAction: inferredAction,
                process: processName,
                processId: processId.ToString(CultureInfo.InvariantCulture),
                note: ""
            );
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
            {
                _openEventsByFile.Remove(key);
            }

            return openInfo;
        }
    }

    private static string BuildOpenFileKey(string fullFileName, int processId, string processName)
    {
        return $"{fullFileName}|{processId}|{processName}";
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

    private bool ShouldMonitorFile(string fileName)
    {
        try
        {
            var settings = GetSettingsSnapshot();

            var ext = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(ext))
                return false;

            if (!settings.ExtensionsToMonitor.Contains(ext))
                return false;

            var fullFileName = GetSafeFullPath(fileName);

            if (string.IsNullOrWhiteSpace(fullFileName))
                return false;

            if (PathExclusions.IsInTempFolder(fullFileName))
                return false;

            var fullCsvLogFileName = Path.GetFullPath(settings.LogFilePath);
            var fullCsvSnapshotFileName = Path.GetFullPath(settings.SnapshotFilePath);

            if (string.Equals(fullFileName, fullCsvLogFileName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(fullFileName, fullCsvSnapshotFileName, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch
        {
            return false;
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
            return null;
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

    private static string FormatFileSize(long? size)
    {
        if (size == null)
            return "";

        return size.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime? value)
    {
        if (value == null)
            return "";

        return value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(DateTime? value)
    {
        if (value == null)
            return "";

        return value.Value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string InferFileAction(long? sizeAtOpening, long? sizeAtClosing, int writeCount)
    {
        if (writeCount > 0)
        {
            if (sizeAtOpening == null && sizeAtClosing == null)
                return "Edit/save action - write detected";

            if (sizeAtOpening == null)
                return "Edit/save action - write detected, opening size unavailable";

            if (sizeAtClosing == null)
                return "Edit/save action - write detected, closing size unavailable";

            if (sizeAtOpening.Value == sizeAtClosing.Value)
                return "Edit/save action - write detected, unchanged final size";

            if (sizeAtClosing.Value > sizeAtOpening.Value)
                return "Edit/save action - file size increased";

            if (sizeAtClosing.Value < sizeAtOpening.Value)
                return "Edit/save action - file size decreased";

            return "Edit/save action - write detected";
        }

        if (sizeAtOpening == null && sizeAtClosing == null)
            return "Unknown action";

        if (sizeAtOpening == null)
            return "Unknown action - opening size unavailable";

        if (sizeAtClosing == null)
            return "Unknown action - closing size unavailable";

        if (sizeAtOpening.Value == sizeAtClosing.Value)
            return "Read / unchanged-size action";

        if (sizeAtClosing.Value > sizeAtOpening.Value)
            return "Possible edit action - file size increased";

        if (sizeAtClosing.Value < sizeAtOpening.Value)
            return "Possible edit action - file size decreased";

        return "Unknown action";
    }

    private void EnsureCsvHeaderExists()
    {
        var settings = GetSettingsSnapshot();

        lock (_logLock)
        {
            EnsureLogFolderExists(settings.LogFilePath);

            var expectedHeader = GetCsvHeaderLine();

            if (!File.Exists(settings.LogFilePath) || new FileInfo(settings.LogFilePath).Length == 0)
            {
                File.WriteAllText(settings.LogFilePath, expectedHeader + Environment.NewLine, Encoding.UTF8);
                return;
            }

            var existingHeader = "";

            try
            {
                existingHeader = File.ReadLines(settings.LogFilePath, Encoding.UTF8).FirstOrDefault() ?? "";
            }
            catch
            {
                return;
            }

            if (string.Equals(existingHeader, expectedHeader, StringComparison.Ordinal))
                return;

            var logFolder = Path.GetDirectoryName(settings.LogFilePath);

            if (string.IsNullOrWhiteSpace(logFolder))
                return;

            var logFileNameWithoutExtension = Path.GetFileNameWithoutExtension(settings.LogFilePath);
            var logFileExtension = Path.GetExtension(settings.LogFilePath);

            if (string.IsNullOrWhiteSpace(logFileExtension))
                logFileExtension = ".csv";

            var backupPath = Path.Combine(
                logFolder,
                logFileNameWithoutExtension + "-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + logFileExtension
            );

            try
            {
                File.Copy(settings.LogFilePath, backupPath, overwrite: false);
                File.WriteAllText(settings.LogFilePath, expectedHeader + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // If backup/header conversion fails, avoid crashing the monitor.
            }
        }
    }

    private static string GetCsvHeaderLine()
    {
        return string.Join(",",
            CsvEscape("File"),
            CsvEscape("Date Opened"),
            CsvEscape("Time Opened"),
            CsvEscape("Size at Opening"),
            CsvEscape("First Save/Write Date"),
            CsvEscape("First Save/Write Time"),
            CsvEscape("Last Save/Write Date"),
            CsvEscape("Last Save/Write Time"),
            CsvEscape("Save/Write Count"),
            CsvEscape("Size at Last Save/Write"),
            CsvEscape("Date Closed"),
            CsvEscape("Time Closed"),
            CsvEscape("Size at Closing"),
            CsvEscape("Inferred Action"),
            CsvEscape("Process"),
            CsvEscape("Process ID"),
            CsvEscape("Note")
        );
    }

    private void WriteCsvRow(
        string file,
        string dateOpened,
        string timeOpened,
        string sizeAtOpening,
        string firstWriteDate,
        string firstWriteTime,
        string lastWriteDate,
        string lastWriteTime,
        string writeCount,
        string sizeAtLastWrite,
        string dateClosed,
        string timeClosed,
        string sizeAtClosing,
        string inferredAction,
        string process,
        string processId,
        string note)
    {
        var settings = GetSettingsSnapshot();

        lock (_logLock)
        {
            try
            {
                EnsureLogFolderExists(settings.LogFilePath);
                EnsureCsvHeaderExists();

                var line = string.Join(",",
                    CsvEscape(file),
                    CsvEscape(dateOpened),
                    CsvEscape(timeOpened),
                    CsvEscape(sizeAtOpening),
                    CsvEscape(firstWriteDate),
                    CsvEscape(firstWriteTime),
                    CsvEscape(lastWriteDate),
                    CsvEscape(lastWriteTime),
                    CsvEscape(writeCount),
                    CsvEscape(sizeAtLastWrite),
                    CsvEscape(dateClosed),
                    CsvEscape(timeClosed),
                    CsvEscape(sizeAtClosing),
                    CsvEscape(inferredAction),
                    CsvEscape(process),
                    CsvEscape(processId),
                    CsvEscape(note)
                );

                File.AppendAllText(settings.LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Avoid crashing the monitor if Excel, antivirus, or another process temporarily locks the log.
            }
        }
    }

    private void WriteErrorToCsv(Exception ex)
    {
        WriteCsvRow(
            file: "ERROR",
            dateOpened: "",
            timeOpened: "",
            sizeAtOpening: "",
            firstWriteDate: "",
            firstWriteTime: "",
            lastWriteDate: "",
            lastWriteTime: "",
            writeCount: "",
            sizeAtLastWrite: "",
            dateClosed: DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeClosed: DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            sizeAtClosing: "",
            inferredAction: "DeskPulse error",
            process: AppInfo.AppName,
            processId: "",
            note: ex.Message
        );
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static void EnsureLogFolderExists(string logFilePath)
    {
        var folder = Path.GetDirectoryName(logFilePath);

        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException("The log file path must include a folder.");

        Directory.CreateDirectory(folder);
    }

    public void OpenLogFileSnapshot()
    {
        var settings = GetSettingsSnapshot();

        lock (_logLock)
        {
            try
            {
                EnsureCsvHeaderExists();

                File.Copy(settings.LogFilePath, settings.SnapshotFilePath, overwrite: true);

                Process.Start(new ProcessStartInfo
                {
                    FileName = settings.SnapshotFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open the Excel log snapshot.\n\nLog file:\n{settings.LogFilePath}\n\nSnapshot file:\n{settings.SnapshotFilePath}\n\nError:\n{ex.Message}",
                    "Could not open Excel log",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _session?.Source.StopProcessing();
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
    }
}

public sealed class FileOpenInfo
{
    public DateTime OpenedTime { get; set; }

    public long? SizeAtOpening { get; set; }

    public int WriteCount { get; set; }

    public DateTime? FirstWriteTime { get; set; }

    public DateTime? LastWriteTime { get; set; }

    public long? SizeAtFirstWrite { get; set; }

    public long? SizeAtLastWrite { get; set; }
}

public sealed class RegisteredFileTypeInfo
{
    public string Extension { get; set; } = "";

    public string Description { get; set; } = "";

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Description))
            return Extension;

        return $"{Extension}  -  {Description}";
    }
}

public static class RegisteredFileTypeReader
{
    public static List<RegisteredFileTypeInfo> GetRegisteredFileTypes()
    {
        var result = new List<RegisteredFileTypeInfo>();

        try
        {
            foreach (var subKeyName in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!IsLikelyFileExtension(subKeyName))
                    continue;

                var description = GetDescriptionForExtension(subKeyName);

                result.Add(new RegisteredFileTypeInfo
                {
                    Extension = subKeyName.Trim(),
                    Description = description
                });
            }
        }
        catch
        {
            // Ignore registry read failures and return what we have.
        }

        return result
            .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLikelyFileExtension(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!value.StartsWith(".", StringComparison.Ordinal))
            return false;

        if (value.Length < 2 || value.Length > 20)
            return false;

        return value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-');
    }

    private static string GetDescriptionForExtension(string extension)
    {
        try
        {
            using var extensionKey = Registry.ClassesRoot.OpenSubKey(extension);

            if (extensionKey == null)
                return "";

            var progId = extensionKey.GetValue("")?.ToString();

            if (!string.IsNullOrWhiteSpace(progId))
            {
                var progIdDescription = GetDescriptionForProgId(progId);

                if (!string.IsNullOrWhiteSpace(progIdDescription))
                    return progIdDescription;
            }

            using var openWithProgIdsKey = extensionKey.OpenSubKey("OpenWithProgids");

            if (openWithProgIdsKey != null)
            {
                foreach (var valueName in openWithProgIdsKey.GetValueNames())
                {
                    if (string.IsNullOrWhiteSpace(valueName))
                        continue;

                    var description = GetDescriptionForProgId(valueName);

                    if (!string.IsNullOrWhiteSpace(description))
                        return description;
                }
            }

            return "";
        }
        catch
        {
            return "";
        }
    }

    private static string GetDescriptionForProgId(string progId)
    {
        try
        {
            using var progIdKey = Registry.ClassesRoot.OpenSubKey(progId);

            if (progIdKey == null)
                return "";

            var description = progIdKey.GetValue("")?.ToString();

            if (string.IsNullOrWhiteSpace(description))
                return "";

            return description.Trim();
        }
        catch
        {
            return "";
        }
    }
}

public static class PathExclusions
{
    public static bool IsInTempFolder(string fullPath)
    {
        try
        {
            var candidate = NormalizeFolderPath(fullPath);

            foreach (var tempFolder in GetTempFolders())
            {
                if (IsSameOrChildPath(candidate, tempFolder))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> GetTempFolders()
    {
        var folders = new List<string>();

        AddFolder(folders, Path.GetTempPath());
        AddFolder(folders, Environment.GetEnvironmentVariable("TEMP"));
        AddFolder(folders, Environment.GetEnvironmentVariable("TMP"));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            AddFolder(folders, Path.Combine(localAppData, "Temp"));
        }

        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(windowsFolder))
        {
            AddFolder(folders, Path.Combine(windowsFolder, "Temp"));
        }

        return folders
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddFolder(List<string> folders, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;

        try
        {
            folders.Add(NormalizeFolderPath(folder));
        }
        catch
        {
            // Ignore invalid temp paths.
        }
    }

    private static string NormalizeFolderPath(string path)
    {
        var full = Path.GetFullPath(path);

        full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return full + Path.DirectorySeparatorChar;
    }

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AppSettings
{
    private const string RegistryPath = @"Software\DeskPulse";
    private const string DefaultLogFileName = "DeskPulse-log.csv";

    public string LogFilePath { get; set; } = GetDefaultLogFilePath();

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

    public string SnapshotFilePath
    {
        get
        {
            var folder = Path.GetDirectoryName(LogFilePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(LogFilePath);
            var extension = Path.GetExtension(LogFilePath);

            if (string.IsNullOrWhiteSpace(folder))
                folder = GetDefaultLogFolderPath();

            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(DefaultLogFileName);

            if (string.IsNullOrWhiteSpace(extension))
                extension = ".csv";

            Directory.CreateDirectory(folder);

            return Path.Combine(folder, fileNameWithoutExtension + "-view" + extension);
        }
    }

    public string ExtensionsAsText
    {
        get
        {
            return string.Join(", ", ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            LogFilePath = LogFilePath,
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

        settings.LogFilePath = ReadString(key, "LogFilePath", settings.LogFilePath);

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

        var logFolder = Path.GetDirectoryName(LogFilePath);

        if (!string.IsNullOrWhiteSpace(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        key.SetValue("AppVersion", AppInfo.AppVersion, RegistryValueKind.String);
        key.SetValue("LogFilePath", LogFilePath, RegistryValueKind.String);
        key.SetValue("ExtensionsToMonitor", ExtensionsAsText, RegistryValueKind.String);
    }

    public static string GetDefaultLogFolderPath()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = AppDomain.CurrentDomain.BaseDirectory;
        }

        var documentsFolder = Path.Combine(userProfile, "Documents");
        var deskPulseFolder = Path.Combine(documentsFolder, "DeskPulse");

        Directory.CreateDirectory(deskPulseFolder);

        return deskPulseFolder;
    }

    public static string GetDefaultLogFilePath()
    {
        return Path.Combine(GetDefaultLogFolderPath(), DefaultLogFileName);
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
            var extension = part.StartsWith(".")
                ? part
                : "." + part;

            result.Add(extension);
        }

        return result;
    }

    private static string ReadString(RegistryKey key, string name, string defaultValue)
    {
        var value = key.GetValue(name);

        if (value == null)
            return defaultValue;

        var text = value.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return defaultValue;

        return text;
    }
}

public sealed class SettingsForm : Form
{
    private readonly ListBox _registeredFileTypesListBox;
    private readonly ListBox _monitoredExtensionsListBox;
    private readonly TextBox _logFilePathTextBox;

    private List<RegisteredFileTypeInfo> _registeredFileTypes = new();

    public SettingsForm()
    {
        var settings = AppSettings.Load();

        Text = $"{AppInfo.AppName} - Settings";
        Width = 900;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var versionLabel = new Label
        {
            Text = $"Version: {AppInfo.AppVersion}",
            Left = 20,
            Top = 20,
            Width = 820,
            Height = 20
        };

        var registeredFileTypesLabel = new Label
        {
            Text = "Registered file types:",
            Left = 20,
            Top = 58,
            Width = 240
        };

        var monitoredExtensionsLabel = new Label
        {
            Text = "Monitored file types:",
            Left = 540,
            Top = 58,
            Width = 240
        };

        _registeredFileTypesListBox = new ListBox
        {
            Left = 20,
            Top = 82,
            Width = 340,
            Height = 250,
            SelectionMode = SelectionMode.MultiExtended,
            AllowDrop = true
        };

        _monitoredExtensionsListBox = new ListBox
        {
            Left = 540,
            Top = 82,
            Width = 320,
            Height = 250,
            SelectionMode = SelectionMode.MultiExtended,
            AllowDrop = true
        };

        _registeredFileTypesListBox.DoubleClick += (_, _) => AddSelectedRegisteredFileTypes();
        _monitoredExtensionsListBox.DoubleClick += (_, _) => RemoveSelectedMonitoredExtensions();

        _registeredFileTypesListBox.MouseDown += ListBoxMouseDown;
        _monitoredExtensionsListBox.MouseDown += ListBoxMouseDown;

        _registeredFileTypesListBox.DragEnter += ListBoxDragEnter;
        _monitoredExtensionsListBox.DragEnter += ListBoxDragEnter;

        _registeredFileTypesListBox.DragDrop += (_, e) => RemoveDraggedExtension(e);
        _monitoredExtensionsListBox.DragDrop += (_, e) => AddDraggedExtension(e);

        var addButton = new Button
        {
            Text = "Add →",
            Left = 390,
            Top = 152,
            Width = 120
        };

        addButton.Click += (_, _) => AddSelectedRegisteredFileTypes();

        var removeButton = new Button
        {
            Text = "← Remove",
            Left = 390,
            Top = 192,
            Width = 120
        };

        removeButton.Click += (_, _) => RemoveSelectedMonitoredExtensions();

        var refreshButton = new Button
        {
            Text = "Refresh list",
            Left = 390,
            Top = 232,
            Width = 120
        };

        refreshButton.Click += (_, _) => LoadRegisteredFileTypes();

        var logFilePathLabel = new Label
        {
            Text = "Log file path:",
            Left = 20,
            Top = 370,
            Width = 170
        };

        _logFilePathTextBox = new TextBox
        {
            Left = 200,
            Top = 366,
            Width = 560,
            Text = settings.LogFilePath
        };

        var browseLogFileButton = new Button
        {
            Text = "Browse...",
            Left = 770,
            Top = 364,
            Width = 90
        };

        browseLogFileButton.Click += (_, _) => BrowseForLogFile();

        var saveButton = new Button
        {
            Text = "Save",
            Left = 680,
            Top = 430,
            Width = 80,
            DialogResult = DialogResult.OK
        };

        saveButton.Click += (_, _) => SaveSettings();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 780,
            Top = 430,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(versionLabel);
        Controls.Add(registeredFileTypesLabel);
        Controls.Add(monitoredExtensionsLabel);
        Controls.Add(_registeredFileTypesListBox);
        Controls.Add(_monitoredExtensionsListBox);
        Controls.Add(addButton);
        Controls.Add(removeButton);
        Controls.Add(refreshButton);
        Controls.Add(logFilePathLabel);
        Controls.Add(_logFilePathTextBox);
        Controls.Add(browseLogFileButton);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        LoadRegisteredFileTypes();
        LoadMonitoredExtensions(settings.ExtensionsToMonitor);
    }

    private void LoadRegisteredFileTypes()
    {
        Cursor = Cursors.WaitCursor;

        try
        {
            _registeredFileTypes = RegisteredFileTypeReader.GetRegisteredFileTypes();

            _registeredFileTypesListBox.BeginUpdate();
            _registeredFileTypesListBox.Items.Clear();

            foreach (var fileType in _registeredFileTypes)
            {
                _registeredFileTypesListBox.Items.Add(fileType);
            }
        }
        finally
        {
            _registeredFileTypesListBox.EndUpdate();
            Cursor = Cursors.Default;
        }
    }

    private void LoadMonitoredExtensions(HashSet<string> monitoredExtensions)
    {
        _monitoredExtensionsListBox.BeginUpdate();
        _monitoredExtensionsListBox.Items.Clear();

        foreach (var extension in monitoredExtensions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            _monitoredExtensionsListBox.Items.Add(extension);
        }

        _monitoredExtensionsListBox.EndUpdate();
    }

    private void AddSelectedRegisteredFileTypes()
    {
        foreach (var selectedItem in _registeredFileTypesListBox.SelectedItems)
        {
            if (selectedItem is RegisteredFileTypeInfo fileType)
            {
                AddExtensionToMonitoredList(fileType.Extension);
            }
        }
    }

    private void RemoveSelectedMonitoredExtensions()
    {
        var itemsToRemove = _monitoredExtensionsListBox.SelectedItems.Cast<object>().ToList();

        foreach (var item in itemsToRemove)
        {
            _monitoredExtensionsListBox.Items.Remove(item);
        }
    }

    private void AddExtensionToMonitoredList(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return;

        extension = extension.Trim();

        if (!extension.StartsWith(".", StringComparison.Ordinal))
            extension = "." + extension;

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            if (string.Equals(item.ToString(), extension, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _monitoredExtensionsListBox.Items.Add(extension);
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
        if (e.Data != null && e.Data.GetDataPresent(typeof(RegisteredFileTypeInfo)))
        {
            e.Effect = DragDropEffects.Move;
            return;
        }

        if (e.Data != null && e.Data.GetDataPresent(typeof(string)))
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

        if (e.Data.GetData(typeof(RegisteredFileTypeInfo)) is RegisteredFileTypeInfo fileType)
        {
            AddExtensionToMonitoredList(fileType.Extension);
            return;
        }

        if (e.Data.GetData(typeof(string)) is string text)
        {
            AddExtensionToMonitoredList(text);
        }
    }

    private void RemoveDraggedExtension(DragEventArgs e)
    {
        if (e.Data == null)
            return;

        if (e.Data.GetData(typeof(string)) is string text)
        {
            foreach (var item in _monitoredExtensionsListBox.Items.Cast<object>().ToList())
            {
                if (string.Equals(item.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    _monitoredExtensionsListBox.Items.Remove(item);
                    return;
                }
            }
        }
    }

    private void BrowseForLogFile()
    {
        var currentPath = _logFilePathTextBox.Text.Trim();

        var initialDirectory = Path.GetDirectoryName(currentPath);

        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
        {
            initialDirectory = AppSettings.GetDefaultLogFolderPath();
        }

        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
        {
            initialDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        var currentFileName = Path.GetFileName(currentPath);

        if (string.IsNullOrWhiteSpace(currentFileName))
        {
            currentFileName = "DeskPulse-log.csv";
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Choose log CSV file",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = initialDirectory,
            FileName = currentFileName,
            OverwritePrompt = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _logFilePathTextBox.Text = dialog.FileName;
        }
    }

    private void SaveSettings()
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            var value = item.ToString();

            if (string.IsNullOrWhiteSpace(value))
                continue;

            value = value.Trim();

            if (!value.StartsWith(".", StringComparison.Ordinal))
                value = "." + value;

            extensions.Add(value);
        }

        if (extensions.Count == 0)
        {
            MessageBox.Show(
                "Please select at least one monitored file type.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var logFilePath = _logFilePathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            MessageBox.Show(
                $"Please enter a log file path.\n\nDefault path:\n{AppSettings.GetDefaultLogFilePath()}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        if (!Path.IsPathFullyQualified(logFilePath))
        {
            MessageBox.Show(
                $"Please enter a full log file path, for example:\n\n{AppSettings.GetDefaultLogFilePath()}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var logFolder = Path.GetDirectoryName(logFilePath);

        if (string.IsNullOrWhiteSpace(logFolder))
        {
            MessageBox.Show(
                $"The log file path must include a folder and filename.\n\nDefault path:\n{AppSettings.GetDefaultLogFilePath()}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            Directory.CreateDirectory(logFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The log folder could not be created or accessed.\n\nFolder:\n{logFolder}\n\nError:\n{ex.Message}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            DialogResult = DialogResult.None;
            return;
        }

        var extension = Path.GetExtension(logFilePath);

        if (string.IsNullOrWhiteSpace(extension))
        {
            logFilePath += ".csv";
        }

        var settings = new AppSettings
        {
            LogFilePath = logFilePath,
            ExtensionsToMonitor = extensions
        };

        settings.Save();
    }
}