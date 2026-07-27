using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskPulse;

public sealed partial class ExcelExportProgressForm : Form
{
    private readonly Func<IProgress<ExportProgressInfo>, int> _exportAction;
    private bool _isRunning;

    public int ExportedCount { get; private set; }
    public Exception? ExportError { get; private set; }

    public ExcelExportProgressForm(string sectionName, Func<IProgress<ExportProgressInfo>, int> exportAction)
    {
        _exportAction = exportAction ?? throw new ArgumentNullException(nameof(exportAction));

        InitializeComponent();
        AppIcon.Apply(this);

        titleLabel.Text = $"Exporting {sectionName} to Excel";
        Shown += async (_, _) => await RunExportAsync();
        FormClosing += ExcelExportProgressForm_FormClosing;
    }

    private async Task RunExportAsync()
    {
        _isRunning = true;
        ControlBox = false;

        try
        {
            var progress = new Progress<ExportProgressInfo>(UpdateProgress);
            UpdateProgress(new ExportProgressInfo(1, "1%   Starting export"));

            ExportedCount = await Task.Run(() => _exportAction(progress));

            UpdateProgress(new ExportProgressInfo(
                100,
                $"100%  Exported {ExportedCount:N0} record(s)"));
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ExportError = ex;
            DialogResult = DialogResult.Abort;
        }
        finally
        {
            _isRunning = false;
            ControlBox = true;
            Close();
        }
    }

    private void UpdateProgress(ExportProgressInfo progressInfo)
    {
        var percent = Math.Clamp(progressInfo.Percent, progressBar.Minimum, progressBar.Maximum);
        progressBar.Value = percent;
        percentLabel.Text = percent.ToString(CultureInfo.InvariantCulture) + "%";
        progressLabel.Text = string.IsNullOrWhiteSpace(progressInfo.Message)
            ? "Working..."
            : progressInfo.Message;
    }

    private void ExcelExportProgressForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isRunning)
            e.Cancel = true;
    }
}
