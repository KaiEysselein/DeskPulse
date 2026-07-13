using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskPulse;

public sealed partial class ExportDateRangeForm : Form
{
    private readonly Action<DateTime, DateTime, IProgress<ExportProgressInfo>> _exportAction;

    public DateTime StartDate => startCalendar.SelectionStart.Date;
    public DateTime EndDate => endCalendar.SelectionStart.Date;

    public ExportDateRangeForm(Action<DateTime, DateTime, IProgress<ExportProgressInfo>> exportAction)
    {
        _exportAction = exportAction ?? throw new ArgumentNullException(nameof(exportAction));
        InitializeComponent();
        AppIcon.Apply(this);

        var firstRecordedDate = DatabaseDateRange.GetFirstRecordedDate(AppSettings.Load().DatabaseFilePath);
        startCalendar.SelectionStart = firstRecordedDate;
        startCalendar.SelectionEnd = firstRecordedDate;
        endCalendar.SelectionStart = DateTime.Today;
        endCalendar.SelectionEnd = DateTime.Today;

        todayOnlyButton.Click += (_, _) => SetTodayOnly();
        exportButton.Click += async (_, _) => await ExportAsync();
    }

    private void SetTodayOnly()
    {
        startCalendar.SelectionStart = DateTime.Today;
        startCalendar.SelectionEnd = DateTime.Today;
        startCalendar.SetDate(DateTime.Today);
    }

    private async Task ExportAsync()
    {
        if (EndDate < StartDate)
        {
            MessageBox.Show(
                "The end day cannot be before the start day.",
                "Invalid export date range",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        SetExportInProgress(true);

        try
        {
            var startDate = StartDate;
            var endDate = EndDate;
            var progress = new Progress<ExportProgressInfo>(UpdateExportProgress);

            await Task.Run(() => _exportAction(startDate, endDate, progress));

            UpdateExportProgress(new ExportProgressInfo(100, "100% Export complete"));

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SetExportInProgress(false);

            MessageBox.Show(
                ex.Message,
                "Could not export activity log",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetExportInProgress(bool inProgress)
    {
        startCalendar.Enabled = !inProgress;
        endCalendar.Enabled = !inProgress;
        todayOnlyButton.Enabled = !inProgress;
        exportButton.Enabled = !inProgress;
        cancelButton.Enabled = !inProgress;
        ControlBox = !inProgress;

        exportButton.Text = inProgress ? "Exporting..." : "Export";
        progressLabel.Visible = inProgress;
        progressBar.Visible = inProgress;

        if (inProgress)
        {
            progressBar.Value = 0;
            progressLabel.Text = "0%   Waiting to export";
        }
    }

    private void UpdateExportProgress(ExportProgressInfo progressInfo)
    {
        var percent = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressInfo.Percent));
        progressBar.Value = percent;

        progressLabel.Text = string.IsNullOrWhiteSpace(progressInfo.Message)
            ? $"{percent}%   Exporting activity log"
            : progressInfo.Message;
    }
}
