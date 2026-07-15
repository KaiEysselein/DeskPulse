using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Forms;

namespace DeskPulse;

internal static class Program
{
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

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayAppContext());
    }
}

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseLoggingMenuItem;
    private bool _loggingPaused;
    private Form? _activeForm;

    public TrayAppContext()
    {
        _trayIcon = new NotifyIcon { Icon = LoadTrayIcon(), Text = AppInfo.AppName, Visible = true };
        _menu = new ContextMenuStrip();
        AddMenuCommand("View Log...", OpenViewLog);
        AddMenuCommand("Settings...", OpenSettings);
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

    private async Task RefreshLoggingStateAsync()
    {
        var response = await ServicePipeClient.SendAsync("LOGGING_STATE");
        ApplyLoggingStateResponse(response, showErrors: false);
    }

    private void ApplyLoggingStateResponse(string response, bool showErrors)
    {
        if (response.Equals("PAUSED", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("OK|PAUSED", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = true;
            _pauseLoggingMenuItem.Text = "Resume Logging";
            _trayIcon.Text = "DeskPulse - Logging paused";
            return;
        }

        if (response.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("OK|ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            _loggingPaused = false;
            _pauseLoggingMenuItem.Text = "Pause Logging";
            _trayIcon.Text = AppInfo.AppName;
            return;
        }

        if (showErrors)
            MessageBox.Show(response, "DeskPulse", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenViewLog()
    {
        OpenSingleForm(new ViewLogForm(() => _ = ServicePipeClient.SendAsync("RELOAD_SETTINGS")));
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
        form.Show();
        RestoreAndActivate(form);
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
        _trayIcon.Visible = false; _trayIcon.Dispose(); _menu.Dispose();
        base.ExitThreadCore();
    }

    private static Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "file-logger.ico");
        if (File.Exists(path)) return new Icon(path);
        return Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application;
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
