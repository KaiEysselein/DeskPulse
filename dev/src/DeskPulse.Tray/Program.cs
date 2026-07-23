using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace DeskPulse;

internal static class Program
{
    private static Mutex? _trayInstanceMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();

        if (args.Any(a => a.Equals("--initialize-settings", StringComparison.OrdinalIgnoreCase)))
        {
            var settings = AppSettings.Load();
            Directory.CreateDirectory(settings.DataFolderPath);
            settings.Save();
            return;
        }

        if (args.Any(a => a.Equals("--enable-startup", StringComparison.OrdinalIgnoreCase)))
        {
            StartupTaskManager.SetEnabled(true);
            var settings = AppSettings.Load();
            settings.StartWithWindows = true;
            settings.Save();
            return;
        }

        if (args.Any(a => a.Equals("--disable-startup", StringComparison.OrdinalIgnoreCase)))
        {
            StartupTaskManager.SetEnabled(false);
            var settings = AppSettings.Load();
            settings.StartWithWindows = false;
            settings.Save();
            return;
        }

        if (TryHandleInstallLifecycleCommand(args))
            return;

        if (TryHandleDiagnosticLoadCommand(args))
            return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Any(a => a.Equals("--administrator-settings", StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsProcessElevated())
            {
                MessageBox.Show(
                    "Administrator settings must be opened through the DeskPulse tray menu and approved through Windows User Account Control.",
                    "DeskPulse Administrator Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Application.Run(new SettingsForm(administratorMode: true));
            return;
        }

        if (args.Any(a => a.Equals("--system-log", StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsProcessElevated())
            {
                MessageBox.Show(
                    "The System Log must be opened through the DeskPulse tray menu and approved through Windows User Account Control.",
                    "DeskPulse System Log",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Application.Run(new ViewLogForm(systemOnly: true));
            return;
        }

        if (args.Any(a => a.Equals("--personal-log", StringComparison.OrdinalIgnoreCase)))
        {
            Application.Run(new ViewLogForm(
                () => _ = ServicePipeClient.SendAsync("RELOAD_SETTINGS")));
            return;
        }

        var mutexName = $"Local\\DeskPulse.Tray.Session.{Process.GetCurrentProcess().SessionId}";
        _trayInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _trayInstanceMutex.Dispose();
            _trayInstanceMutex = null;
            return;
        }

        try
        {
            Application.Run(new TrayAppContext());
        }
        finally
        {
            try { _trayInstanceMutex.ReleaseMutex(); } catch { }
            _trayInstanceMutex.Dispose();
            _trayInstanceMutex = null;
        }
    }

    private static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }


    private static bool TryHandleInstallLifecycleCommand(string[] args)
    {
        if (args.Length != 4 || !args[0].Equals("--record-install-lifecycle", StringComparison.OrdinalIgnoreCase))
            return false;

        var action = args[1].Trim();
        var previousVersion = args[2].Trim();
        var newVersion = args[3].Trim();
        var command = $"INSTALL_LIFECYCLE|{action}|{previousVersion}|{newVersion}|{Environment.UserName}";

        for (var attempt = 0; attempt < 15; attempt++)
        {
            var response = ServicePipeClient.SendAsync(command).GetAwaiter().GetResult();
            if (response.Equals("OK", StringComparison.OrdinalIgnoreCase))
                return true;

            Thread.Sleep(1000);
        }

        try
        {
            var logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskPulse");
            Directory.CreateDirectory(logFolder);
            File.AppendAllText(Path.Combine(logFolder, "installer-lifecycle-errors.log"),
                DateTime.Now + " Failed to record installation lifecycle event: " + action + " " + previousVersion + " -> " + newVersion + Environment.NewLine);
        }
        catch { }

        return true;
    }

    private static bool TryHandleDiagnosticLoadCommand(string[] args)
    {
        if (args.Length == 0)
            return false;

        string? command = null;
        string title = "DeskPulse Diagnostic Load Test";

        if (args[0].Equals("--stop-service-load-test", StringComparison.OrdinalIgnoreCase) ||
            args[0].Equals("--stop-load", StringComparison.OrdinalIgnoreCase))
        {
            command = "STOP_LOAD_TEST";
        }
        else if (args[0].Equals("--service-load-status", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("--load-status", StringComparison.OrdinalIgnoreCase))
        {
            command = "LOAD_TEST_STATUS";
        }
        else if (args[0].Equals("--test-service-cpu", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadPercentAndDuration(args, out var percent, out var duration, out var error))
            {
                MessageBox.Show(error, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true;
            }
            command = BuildLoadCommand(percent, 0, duration);
        }
        else if (args[0].Equals("--test-service-memory", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadPercentAndDuration(args, out var percent, out var duration, out var error))
            {
                MessageBox.Show(error, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true;
            }
            command = BuildLoadCommand(0, percent, duration);
        }
        else if (args[0].Equals("--test-service-load", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("--load", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("-l", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadCombinedLoad(args, out var cpu, out var memory, out var duration, out var error))
            {
                MessageBox.Show(error, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true;
            }
            command = BuildLoadCommand(cpu, memory, duration);
        }
        else
        {
            return false;
        }

        var response = ServicePipeClient.SendAsync(command).GetAwaiter().GetResult();
        var isError = response.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase) ||
                      response.StartsWith("DeskPulse service is unavailable", StringComparison.OrdinalIgnoreCase);

        if (!isError && response.StartsWith("OK|STARTED|", StringComparison.OrdinalIgnoreCase))
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DiagnosticLoadTestForm());
        }
        else
        {
            MessageBox.Show(FormatDiagnosticResponse(response), title, MessageBoxButtons.OK,
                isError ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        return true;
    }

    private static bool TryReadPercentAndDuration(string[] args, out double percent, out int duration, out string error)
    {
        percent = 0;
        duration = 0;
        error = "";
        if (args.Length != 3 ||
            !double.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out percent) ||
            !int.TryParse(args[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out duration))
        {
            error = "Use: DeskPulse.Tray.exe --test-service-cpu <percent> <seconds>\n\nor\n\nDeskPulse.Tray.exe --test-service-memory <percent> <seconds>";
            return false;
        }
        return ValidateLoadValues(percent, 0, duration, out error);
    }

    private static bool TryReadCombinedLoad(string[] args, out double cpu, out double memory, out int duration, out string error)
    {
        cpu = 0;
        memory = 0;
        duration = 60;
        error = "";

        for (var i = 1; i < args.Length; i++)
        {
            if (i + 1 >= args.Length)
            {
                error = "Every load-test option requires a value.";
                return false;
            }

            var option = args[i];
            var value = args[++i];
            if (option.Equals("--cpu", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cpu))
                { error = "The CPU percentage is invalid."; return false; }
            }
            else if (option.Equals("--memory", StringComparison.OrdinalIgnoreCase) || option.Equals("--ram", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out memory))
                { error = "The memory percentage is invalid."; return false; }
            }
            else if (option.Equals("--duration", StringComparison.OrdinalIgnoreCase) || option.Equals("--seconds", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out duration))
                { error = "The duration is invalid."; return false; }
            }
            else
            {
                error = "Unknown option: " + option;
                return false;
            }
        }

        return ValidateLoadValues(cpu, memory, duration, out error);
    }

    private static bool ValidateLoadValues(double cpu, double memory, int duration, out string error)
    {
        error = "";
        if (cpu < 0 || memory < 0 || cpu > 50 || memory > 50)
        {
            error = "CPU and memory targets must each be between 0% and 50%. Values above 50% are never permitted.";
            return false;
        }
        if (cpu <= 0 && memory <= 0)
        {
            error = "Specify a CPU or memory target greater than zero.";
            return false;
        }
        if (duration < 1 || duration > 300)
        {
            error = "Duration must be between 1 and 300 seconds.";
            return false;
        }
        return true;
    }

    private static string BuildLoadCommand(double cpu, double memory, int duration) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"START_LOAD_TEST|{cpu}|{memory}|{duration}");

    private static string FormatDiagnosticResponse(string response)
    {
        if (response.StartsWith("OK|STARTED|", StringComparison.OrdinalIgnoreCase))
            return "The controlled diagnostic service load test has started.\n\n" + response.Replace('|', ' ');
        if (response.Equals("OK|STOPPING", StringComparison.OrdinalIgnoreCase))
            return "The diagnostic service load test is stopping.";
        if (response.Equals("OK|NOT_RUNNING", StringComparison.OrdinalIgnoreCase))
            return "No diagnostic service load test is running.";
        if (response.StartsWith("OK|", StringComparison.OrdinalIgnoreCase))
            return response[3..];
        if (response.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase))
            return response[6..];
        return response;
    }
}

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly Icon _normalTrayIcon;
    private readonly Icon _pausedTrayIcon;
    private readonly Icon _warningTrayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseLoggingMenuItem;
    private bool _loggingPaused;
    private Form? _activeForm;
    private readonly System.Windows.Forms.Timer _focusLossTimer;
    private readonly System.Windows.Forms.Timer _safetyTimer;

    public TrayAppContext()
    {
        _normalTrayIcon = AppIcon.Load(AppIconState.Normal);
        _pausedTrayIcon = AppIcon.Load(AppIconState.Paused);
        _warningTrayIcon = AppIcon.Load(AppIconState.Warning);
        _trayIcon = new NotifyIcon { Icon = _normalTrayIcon, Text = AppInfo.AppName, Visible = true };
        _focusLossTimer = new System.Windows.Forms.Timer { Interval = 180 };
        _safetyTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _safetyTimer.Tick += async (_, _) => await RefreshSafetyStateAsync();
        _safetyTimer.Start();
        _focusLossTimer.Tick += (_, _) => CloseActiveFormIfFocusWasLost();
        _menu = new ContextMenuStrip();
        AddMenuCommand("Personal Log...", OpenViewLog);
        AddMenuCommand("System Log (Administrator)...", OpenSystemLog);
        AddMenuCommand("Settings...", OpenSettings);
        AddMenuCommand("Administrator settings...", OpenAdministratorSettings);
        _pauseLoggingMenuItem = new ToolStripMenuItem("Pause Logging");
        _pauseLoggingMenuItem.Click += async (_, _) => await ToggleLoggingAsync();
        _menu.Items.Add(_pauseLoggingMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        var serviceStatusItem = new ToolStripMenuItem("Service status");
        serviceStatusItem.Click += async (_, _) =>
        {
            _menu.Close();
            await Task.Yield();
            MessageBox.Show(await ServicePipeClient.GetStatusAsync(), "DeskPulse Service");
        };
        _menu.Items.Add(serviceStatusItem);
        AddMenuCommand("About", () => OpenSingleForm(new AboutForm()));
        AddMenuCommand("Quit DeskPulse", ExitThread);
        _trayIcon.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _menu.Show(Cursor.Position); };
        _ = ServicePipeClient.SendAsync("TRAY_STARTED");

        var stateTimer = new System.Windows.Forms.Timer { Interval = 750 };
        stateTimer.Tick += async (_, _) =>
        {
            stateTimer.Stop();
            stateTimer.Dispose();
            await RefreshLoggingStateAsync();
        };
        stateTimer.Start();
    }


    private void AddMenuCommand(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) =>
        {
            _menu.Close();
            _menu.BeginInvoke(new Action(action));
        };
        _menu.Items.Add(item);
    }

    private async Task ToggleLoggingAsync()
    {
        _pauseLoggingMenuItem.Enabled = false;
        try
        {
            var response = await ServicePipeClient.SendAsync(_loggingPaused ? "RESUME_LOGGING" : "PAUSE_LOGGING");
            ApplyLoggingStateResponse(response, showErrors: true);
        }
        finally
        {
            _pauseLoggingMenuItem.Enabled = true;
        }
    }

    private async Task RefreshSafetyStateAsync()
    {
        var response = await ServicePipeClient.SendAsync("SAFETY_STATUS");
        if (!response.StartsWith("OK|", StringComparison.OrdinalIgnoreCase)) return;
        var state = response.Split('|').FirstOrDefault(x => x.StartsWith("STATE=", StringComparison.OrdinalIgnoreCase))?[6..] ?? "Normal";
        if (state.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            SetTrayState(AppIconState.Warning, "DeskPulse - Service resource warning");
        else if (state.Equals("CriticalPaused", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = true; _pauseLoggingMenuItem.Text = "Resume Logging";
            SetTrayState(AppIconState.Warning, "DeskPulse - Critical safety pause");
        }
        else if (!_loggingPaused) SetTrayState(AppIconState.Normal, AppInfo.AppName);
    }

    private async Task RefreshLoggingStateAsync()
    {
        var response = await ServicePipeClient.SendAsync("LOGGING_STATE");
        ApplyLoggingStateResponse(response, showErrors: false);
    }

    private void ApplyLoggingStateResponse(string response, bool showErrors)
    {
        if (response.Equals("CRITICAL_PAUSED", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = true;
            _pauseLoggingMenuItem.Text = "Resume Logging";
            SetTrayState(AppIconState.Warning, "DeskPulse - Critical safety pause");
            return;
        }

        if (response.Equals("PAUSED", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("OK|PAUSED", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = true;
            _pauseLoggingMenuItem.Text = "Resume Logging";
            SetTrayState(AppIconState.Paused, "DeskPulse - Logging paused");
            return;
        }

        if (response.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("OK|ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = false;
            _pauseLoggingMenuItem.Text = "Pause Logging";
            SetTrayState(AppIconState.Normal, AppInfo.AppName);
            return;
        }

        SetTrayState(AppIconState.Warning, "DeskPulse - Service attention required");

        if (showErrors)
            MessageBox.Show(response, "DeskPulse", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static void OpenViewLog()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("DeskPulse could not determine its executable path.");

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--personal-log",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "DeskPulse could not open the personal log.\n\n" + ex.Message,
                "DeskPulse",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void OpenSystemLog()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("DeskPulse could not determine its executable path.");

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--system-log",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // The user cancelled the Windows UAC prompt.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "DeskPulse could not open the system log.\n\n" + ex.Message,
                "DeskPulse",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenSettings()
    {
        var form = new SettingsForm();
        form.FormClosed += (_, _) =>
        {
            if (form.DialogResult == DialogResult.OK)
                _ = ServicePipeClient.SendAsync("RELOAD_SETTINGS");
        };
        OpenSingleForm(form);
    }

    private static void OpenAdministratorSettings()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("DeskPulse could not determine its executable path.");

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--administrator-settings",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // The user cancelled the Windows UAC prompt.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "DeskPulse could not open Administrator settings.\n\n" + ex.Message,
                "DeskPulse",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenSingleForm(Form form)
    {
        if (_activeForm != null && !_activeForm.IsDisposed && _activeForm.GetType() == form.GetType())
        {
            form.Dispose();
            RestoreAndActivate(_activeForm);
            return;
        }

        CloseActiveForm();

        _activeForm = form;
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_activeForm, form))
                _activeForm = null;
            form.Dispose();
        };

        form.StartPosition = FormStartPosition.CenterScreen;

        // Settings and View Log are persistent working windows. They must never be
        // dismissed merely because a button opens a picker, native dialog, or workbook.
        // Focus-loss auto-close is reserved for the lightweight tray forms.
        if (form is not SettingsForm && form is not ViewLogForm)
            form.Deactivate += ActiveForm_Deactivate;

        form.FormClosed += (_, _) => _focusLossTimer.Stop();
        form.Show();
        RestoreAndActivate(form);
    }


    private void ActiveForm_Deactivate(object? sender, EventArgs e)
    {
        _focusLossTimer.Stop();
        _focusLossTimer.Start();
    }

    private void CloseActiveFormIfFocusWasLost()
    {
        _focusLossTimer.Stop();

        var form = _activeForm;
        if (form is null || form.IsDisposed || !form.Visible || form.ContainsFocus || !form.Enabled)
            return;

        // Working windows contain long-running and modal actions. The user closes them
        // explicitly; native dialogs such as SaveFileDialog are not in OpenForms.
        if (form is SettingsForm || form is ViewLogForm)
            return;

        // Keep the tray-opened form alive while one of its child dialogs is active.
        foreach (Form openForm in Application.OpenForms)
        {
            if (openForm.IsDisposed || !openForm.Visible || ReferenceEquals(openForm, form))
                continue;

            for (Form? owner = openForm.Owner; owner != null; owner = owner.Owner)
            {
                if (ReferenceEquals(owner, form))
                    return;
            }
        }

        // A different DeskPulse window may temporarily have focus while an action completes.
        var active = Form.ActiveForm;
        if (active != null && active.GetType().Namespace == typeof(TrayAppContext).Namespace)
            return;

        CloseActiveForm();
    }

    private static void RestoreAndActivate(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Normal;

        if (!form.Visible)
            form.Show();

        form.ShowInTaskbar = true;
        form.BringToFront();
        form.Activate();
        form.TopMost = true;
        form.TopMost = false;
        form.Focus();
    }

    private void CloseActiveForm()
    {
        var form = _activeForm;
        _activeForm = null;

        if (form is null || form.IsDisposed)
            return;

        form.Close();
        if (!form.IsDisposed)
            form.Dispose();
    }

    protected override void ExitThreadCore()
    {
        try { ServicePipeClient.SendAsync("TRAY_STOPPED").GetAwaiter().GetResult(); } catch { }
        CloseActiveForm();
        _focusLossTimer.Stop(); _focusLossTimer.Dispose();
        _safetyTimer.Stop(); _safetyTimer.Dispose();
        _trayIcon.Visible = false; _trayIcon.Dispose();
        _normalTrayIcon.Dispose(); _pausedTrayIcon.Dispose(); _warningTrayIcon.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }

    private void SetTrayState(AppIconState state, string tooltip)
    {
        _trayIcon.Icon = state switch
        {
            AppIconState.Paused => _pausedTrayIcon,
            AppIconState.Warning => _warningTrayIcon,
            _ => _normalTrayIcon
        };
        _trayIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }
}

public static class ServicePipeClient
{
    public static async Task<string> SendAsync(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(command);
            return await reader.ReadLineAsync() ?? "No response from service.";
        }
        catch (Exception ex)
        {
            return "DeskPulse service is unavailable. " + ex.Message;
        }
    }
    public static Task<string> GetStatusAsync() => SendAsync("STATUS");

    public static async Task<MaintenanceExclusionCleanupResult> RunDatabaseHousekeepingAsync(
        IProgress<ExportProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(30));
            await client.ConnectAsync(timeout.Token);

            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

            progress?.Report(new ExportProgressInfo(10, "10%  Sending housekeeping request to Windows service"));
            await writer.WriteLineAsync("CLEAN_DATABASE_CURRENT_RULES");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await reader.ReadLineAsync(timeout.Token);
                if (response == null)
                    throw new InvalidOperationException("The DeskPulse service closed the housekeeping connection unexpectedly.");

                var parts = response.Split('|');
                if (parts.Length >= 3 && parts[0].Equals("PROGRESS", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(parts[1], out var percent))
                {
                    progress?.Report(new ExportProgressInfo(percent, string.Join("|", parts.Skip(2))));
                    continue;
                }

                if (parts.Length >= 5 && parts[0].Equals("RESULT", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(parts[2], out var activityDeleted) &&
                    long.TryParse(parts[3], out var programDeleted) &&
                    long.TryParse(parts[4], out var userDeleted))
                {
                    return new MaintenanceExclusionCleanupResult
                    {
                        ActivityRecordsDeleted = activityDeleted,
                        ProgramRecordsDeleted = programDeleted,
                        UserRecordsDeleted = userDeleted
                    };
                }

                if (parts.Length >= 3 && parts[0].Equals("RESULT", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(string.Join("|", parts.Skip(2)));

                throw new InvalidOperationException(response);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Database housekeeping timed out while waiting for the DeskPulse service.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("DeskPulse service is unavailable. " + ex.Message, ex);
        }
    }



    public static async Task<MaintenanceExclusionCleanupResult> RepairHistoricalDataAsync(
        IProgress<ExportProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(30));
            await client.ConnectAsync(timeout.Token);

            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync("REPAIR_HISTORICAL_DATA");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await reader.ReadLineAsync(timeout.Token);
                if (response == null)
                    throw new InvalidOperationException("The DeskPulse service closed the repair connection unexpectedly.");

                var parts = response.Split('|');
                if (parts.Length >= 3 && parts[0].Equals("PROGRESS", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(parts[1], out var percent))
                {
                    progress?.Report(new ExportProgressInfo(percent, string.Join("|", parts.Skip(2))));
                    continue;
                }

                if (parts.Length >= 3 && parts[0].Equals("RESULT", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(parts[2], out var repaired))
                    return new MaintenanceExclusionCleanupResult { ActivityRecordsRepaired = repaired };

                if (parts.Length >= 3 && parts[0].Equals("RESULT", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(string.Join("|", parts.Skip(2)));

                throw new InvalidOperationException(response);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Historical data repair timed out while waiting for the DeskPulse service.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("DeskPulse service is unavailable. " + ex.Message, ex);
        }
    }


    public static async Task<long> DeleteRecordsAsync(string tableName, IReadOnlyList<long> ids)
    {
        if (ids == null || ids.Count == 0)
            return 0;
        var payload = string.Join(",", ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return ParseAffectedCount(await SendAsync("DELETE_RECORDS|" + tableName + "|" + payload, TimeSpan.FromMinutes(5)));
    }

    public static async Task<long> ClearTableAsync(string tableName)
    {
        return ParseAffectedCount(await SendAsync("CLEAR_TABLE|" + tableName, TimeSpan.FromMinutes(10)));
    }

    public static async Task<long> ClearAllRecordsAsync()
    {
        return ParseAffectedCount(await SendAsync("CLEAR_ALL_RECORDS", TimeSpan.FromMinutes(10)));
    }

    private static long ParseAffectedCount(string response)
    {
        var parts = response.Split('|');
        if (parts.Length >= 2 && parts[0].Equals("OK", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var affected))
            return affected;

        if (parts.Length >= 2 && parts[0].Equals("ERROR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.Join("|", parts.Skip(1)));

        throw new InvalidOperationException(response);
    }

    private static async Task<string> SendAsync(string command, TimeSpan operationTimeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(operationTimeout);
            await client.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(command);
            return await reader.ReadLineAsync(timeout.Token) ?? "No response from service.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("DeskPulse service is unavailable. " + ex.Message, ex);
        }
    }
}
