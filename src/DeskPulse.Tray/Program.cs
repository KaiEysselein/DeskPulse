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
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SQLitePCL.Batteries_V2.Init();
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
        _menu.Items.Add("View Log...", null, (_, _) => OpenViewLog());
        _menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        _pauseLoggingMenuItem = new ToolStripMenuItem("Pause Logging");
        _pauseLoggingMenuItem.Click += async (_, _) => await ToggleLoggingAsync();
        _menu.Items.Add(_pauseLoggingMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Service status", null, async (_, _) => MessageBox.Show(await ServicePipeClient.GetStatusAsync(), "DeskPulse Service"));
        _menu.Items.Add("About", null, (_, _) => OpenSingleForm(new AboutForm()));
        _menu.Items.Add("Exit tray", null, (_, _) => ExitThread());
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
        form.Activate();
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
}
