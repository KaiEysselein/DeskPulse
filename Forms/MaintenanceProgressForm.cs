using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskPulse;

public sealed partial class MaintenanceProgressForm : Form
{
    private readonly string _caption;
    private readonly string _operationTitle;
    private readonly Func<IProgress<ExportProgressInfo>, MaintenanceExclusionCleanupResult> _action;

    public MaintenanceExclusionCleanupResult? Result { get; private set; }

    public MaintenanceProgressForm(
        string caption,
        string operationTitle,
        Func<IProgress<ExportProgressInfo>, MaintenanceExclusionCleanupResult> action)
    {
        _caption = string.IsNullOrWhiteSpace(caption) ? "DeskPulse Maintenance" : caption;
        _operationTitle = string.IsNullOrWhiteSpace(operationTitle) ? "Working" : operationTitle;
        _action = action ?? throw new ArgumentNullException(nameof(action));

        InitializeComponent();

        Text = _caption;
        titleLabel.Text = _operationTitle;
        closeButton.Click += (_, _) => Close();
        Shown += async (_, _) => await RunActionAsync();
    }

    private async Task RunActionAsync()
    {
        try
        {
            var progress = new Progress<ExportProgressInfo>(UpdateProgress);
            UpdateProgress(new ExportProgressInfo(1, "1%   Starting"));

            Result = await Task.Run(() => _action(progress));

            UpdateProgress(new ExportProgressInfo(100, "100% Complete"));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            closeButton.Enabled = true;
            ControlBox = true;

            MessageBox.Show(
                "The maintenance action could not be completed.\n\n" + ex.Message,
                _caption,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateProgress(ExportProgressInfo progressInfo)
    {
        var percent = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressInfo.Percent));
        progressBar.Value = percent;

        progressLabel.Text = string.IsNullOrWhiteSpace(progressInfo.Message)
            ? percent.ToString(CultureInfo.InvariantCulture) + "%   Working"
            : progressInfo.Message;
    }
}
