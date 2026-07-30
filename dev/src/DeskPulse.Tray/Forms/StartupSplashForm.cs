#nullable enable

using System.Drawing;
using System.Windows.Forms;

namespace DeskPulse;

internal sealed class StartupSplashForm : Form
{
    private readonly System.Windows.Forms.Timer _closeTimer = new() { Interval = 3000 };
    private readonly Label _message;

    public StartupSplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        ClientSize = new Size(330, 92);
        Padding = new Padding(12);
        AppIcon.Apply(this);
        AutoScroll = false;

        var icon = new PictureBox
        {
            Image = AppIcon.Load().ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(12, 18),
            Size = new Size(52, 52)
        };
        var title = new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold),
            Location = new Point(76, 15),
            Text = $"DeskPulse {AppInfo.Version}"
        };
        _message = new Label
        {
            AutoSize = true,
            Location = new Point(76, 43),
            Text = "Checking activity monitoring status..."
        };
        Controls.AddRange(new Control[] { icon, title, _message });

        foreach (Control control in Controls)
            control.Click += (_, _) => Close();
        Click += (_, _) => Close();
        _closeTimer.Tick += (_, _) => Close();
        Shown += (_, _) => _closeTimer.Start();
        FormClosed += (_, _) => _closeTimer.Dispose();
    }

    public void SetStatus(string status)
    {
        if (!IsDisposed)
            _message.Text = status;
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        base.OnShown(e);
    }
}
