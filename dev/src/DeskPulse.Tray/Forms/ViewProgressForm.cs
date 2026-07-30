#nullable enable

using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DeskPulse;

internal sealed class ViewProgressForm : Form
{
    public ViewProgressForm(string message)
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(390, 92);
        Text = "DeskPulse";
        AppIcon.Apply(this);
        AutoScroll = false;

        var label = new Label
        {
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(358, 24),
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var progress = new ProgressBar
        {
            Location = new Point(16, 50),
            Size = new Size(358, 20),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 22
        };
        Controls.AddRange(new Control[] { label, progress });
    }

}

internal sealed class ViewProgressSession : IDisposable
{
    private readonly Thread _thread;
    private ViewProgressForm? _form;

    private ViewProgressSession(string message)
    {
        var shown = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _form = new ViewProgressForm(message);
            _form.Shown += (_, _) => shown.Set();
            Application.Run(_form);
        })
        {
            IsBackground = true,
            Name = "DeskPulse view progress"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        shown.Wait(1000);
    }

    public static ViewProgressSession Start(string message) => new(message);

    public void Dispose()
    {
        var form = _form;
        if (form != null && form.IsHandleCreated && !form.IsDisposed)
        {
            try { form.BeginInvoke(new Action(form.Close)); }
            catch { }
        }
        _thread.Join(1500);
    }
}
