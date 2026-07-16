using System.Globalization;

namespace DeskPulse;

internal sealed class DiagnosticLoadTestForm : Form
{
    private readonly ProgressBar _timeProgress;
    private readonly Label _timeValue;
    private readonly Label _statusValue;
    private readonly Label _cpuTargetValue;
    private readonly Label _cpuCurrentValue;
    private readonly Label _memoryTargetValue;
    private readonly Label _allocatedValue;
    private readonly Label _workingSetValue;
    private readonly Label _systemMemoryValue;
    private readonly Button _stopButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private bool _refreshing;
    private bool _finished;

    public DiagnosticLoadTestForm()
    {
        Text = "DeskPulse Diagnostic Load Test";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        ClientSize = new Size(500, 390);
        Icon = AppIcon.Load(AppIconState.Normal);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 12,
            AutoSize = false
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = "DeskPulse Service Load Test",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        main.Controls.Add(heading, 0, 0);
        main.SetColumnSpan(heading, 2);

        var purpose = new Label
        {
            Text = "This controlled load test is used to verify the DeskPulse service CPU/RAM safeguards and safety-pause response.",
            AutoSize = true, MaximumSize = new Size(455, 0), Anchor = AnchorStyles.Left
        };
        main.Controls.Add(purpose, 0, 1); main.SetColumnSpan(purpose, 2);

        _timeProgress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        main.Controls.Add(_timeProgress, 0, 2);
        main.SetColumnSpan(_timeProgress, 2);

        _timeValue = AddRow(main, 3, "Elapsed / duration:");
        _statusValue = AddRow(main, 4, "Status:");
        _cpuTargetValue = AddRow(main, 5, "CPU target:");
        _cpuCurrentValue = AddRow(main, 6, "Current service CPU:");
        _memoryTargetValue = AddRow(main, 7, "Memory target:");
        _allocatedValue = AddRow(main, 8, "Allocated test memory:");
        _workingSetValue = AddRow(main, 9, "Current service RAM:");
        _systemMemoryValue = AddRow(main, 10, "System RAM usage:");

        _stopButton = new Button
        {
            Text = "Stop Test",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            MinimumSize = new Size(110, 32)
        };
        _stopButton.Click += StopButton_Click;
        main.Controls.Add(_stopButton, 1, 11);

        Controls.Add(main);
        AcceptButton = _stopButton;

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        Shown += async (_, _) =>
        {
            await RefreshStatusAsync();
            _refreshTimer.Start();
        };
        FormClosed += (_, _) => _refreshTimer.Dispose();
    }

    private static Label AddRow(TableLayoutPanel panel, int row, string caption)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 9));
        var captionLabel = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left };
        var valueLabel = new Label { Text = "—", AutoSize = true, Anchor = AnchorStyles.Left };
        panel.Controls.Add(captionLabel, 0, row);
        panel.Controls.Add(valueLabel, 1, row);
        return valueLabel;
    }

    private async Task RefreshStatusAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        try
        {
            var response = await ServicePipeClient.SendAsync("LOAD_TEST_STATUS");
            if (!TryParseStatus(response, out var status))
            {
                _statusValue.Text = response.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase)
                    ? response[6..]
                    : response;
                return;
            }

            var duration = Math.Max(0, status.DurationSeconds);
            var elapsed = Math.Clamp(status.ElapsedSeconds, 0, duration > 0 ? duration : status.ElapsedSeconds);
            var progress = duration > 0 ? (int)Math.Clamp(Math.Round(elapsed / duration * 100.0), 0, 100) : 0;
            _timeProgress.Value = progress;
            _timeValue.Text = $"{FormatDuration(elapsed)} / {FormatDuration(duration)} ({progress}%)";
            _statusValue.Text = string.IsNullOrWhiteSpace(status.Message) ? status.State : $"{status.State} — {status.Message}";
            _cpuTargetValue.Text = $"{status.CpuTarget:0.##}%";
            _cpuCurrentValue.Text = $"{status.CurrentCpu:0.0}%";
            _memoryTargetValue.Text = $"{status.MemoryTarget:0.##}%";
            _allocatedValue.Text = FormatBytes(status.AllocatedBytes);
            _workingSetValue.Text = FormatBytes(status.WorkingSetBytes);
            _systemMemoryValue.Text = $"{status.SystemMemoryUsed:0.0}%";

            _finished = status.State is "Completed" or "Cancelled" or "Error" or "Idle";
            if (_finished)
            {
                _refreshTimer.Stop();
                _stopButton.Text = "Close";
                _stopButton.Enabled = true;
            }
            else if (status.State == "Stopping")
            {
                _stopButton.Text = "Stopping...";
                _stopButton.Enabled = false;
            }
        }
        catch (Exception ex)
        {
            _statusValue.Text = "Unable to read service status: " + ex.Message;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async void StopButton_Click(object? sender, EventArgs e)
    {
        if (_finished)
        {
            Close();
            return;
        }

        _stopButton.Enabled = false;
        _stopButton.Text = "Stopping...";
        var response = await ServicePipeClient.SendAsync("STOP_LOAD_TEST");
        if (response.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, response[6..], Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _stopButton.Enabled = true;
            _stopButton.Text = "Stop Test";
            return;
        }
        await RefreshStatusAsync();
    }

    private static bool TryParseStatus(string response, out LoadTestStatus status)
    {
        status = new LoadTestStatus();
        if (!response.StartsWith("OK|", StringComparison.OrdinalIgnoreCase))
            return false;

        var values = response.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        values.TryGetValue("STATE", out status.State);
        values.TryGetValue("MESSAGE", out status.Message);
        status.CpuTarget = ParseDouble(values, "CPU_TARGET");
        status.MemoryTarget = ParseDouble(values, "MEMORY_TARGET");
        status.DurationSeconds = (int)ParseDouble(values, "DURATION");
        status.ElapsedSeconds = ParseDouble(values, "ELAPSED");
        status.CurrentCpu = ParseDouble(values, "CPU_CURRENT");
        status.WorkingSetBytes = ParseLong(values, "WORKING_SET");
        status.AllocatedBytes = ParseLong(values, "ALLOCATED");
        status.SystemMemoryUsed = ParseDouble(values, "SYSTEM_MEMORY_USED");
        status.State ??= "Unknown";
        status.Message ??= "";
        return true;
    }

    private static double ParseDouble(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static long ParseLong(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string FormatDuration(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        var gb = bytes / 1024d / 1024d / 1024d;
        return gb >= 1 ? $"{gb:0.00} GB" : $"{bytes / 1024d / 1024d:0.0} MB";
    }

    private sealed class LoadTestStatus
    {
        public string? State;
        public string? Message;
        public double CpuTarget;
        public double MemoryTarget;
        public int DurationSeconds;
        public double ElapsedSeconds;
        public double CurrentCpu;
        public long WorkingSetBytes;
        public long AllocatedBytes;
        public double SystemMemoryUsed;
    }
}
