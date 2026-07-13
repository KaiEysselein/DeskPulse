using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskPulse;

public sealed partial class RuleCleanupProgressForm : Form
{
    private readonly string _caption;
    private readonly string _operationTitle;
    private readonly Func<IProgress<ExportProgressInfo>, CancellationToken, MaintenanceExclusionCleanupResult> _action;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly bool _canCancel;
    private bool _isRunning;

    public MaintenanceExclusionCleanupResult? Result { get; private set; }

    public RuleCleanupProgressForm(
        string caption,
        string operationTitle,
        Func<IProgress<ExportProgressInfo>, MaintenanceExclusionCleanupResult> action)
        : this(caption, operationTitle, (progress, _) => action(progress), false)
    {
    }

    public RuleCleanupProgressForm(
        string caption,
        string operationTitle,
        Func<IProgress<ExportProgressInfo>, CancellationToken, MaintenanceExclusionCleanupResult> action)
        : this(caption, operationTitle, action, true)
    {
    }

    private RuleCleanupProgressForm(
        string caption,
        string operationTitle,
        Func<IProgress<ExportProgressInfo>, CancellationToken, MaintenanceExclusionCleanupResult> action,
        bool canCancel)
    {
        _caption = string.IsNullOrWhiteSpace(caption) ? "DeskPulse Cleanup" : caption;
        _operationTitle = string.IsNullOrWhiteSpace(operationTitle) ? "Working" : operationTitle;
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _canCancel = canCancel;

        InitializeComponent();
        AppIcon.Apply(this);

        Text = _caption;
        titleLabel.Text = _operationTitle;
        actionButton.Text = _canCancel ? "Cancel" : "Close";
        actionButton.Enabled = _canCancel;
        actionButton.Click += ActionButton_Click;
        FormClosing += RuleCleanupProgressForm_FormClosing;
        Shown += async (_, _) => await RunActionAsync();
    }

    private void ActionButton_Click(object? sender, EventArgs e)
    {
        if (!_isRunning)
        {
            Close();
            return;
        }

        if (!_canCancel || _cancellationTokenSource.IsCancellationRequested)
            return;

        actionButton.Enabled = false;
        actionButton.Text = "Cancelling...";
        progressLabel.Text = "Cancellation requested. Finishing the current database step...";
        _cancellationTokenSource.Cancel();
    }

    private void RuleCleanupProgressForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isRunning)
            return;

        e.Cancel = true;

        if (_canCancel && !_cancellationTokenSource.IsCancellationRequested)
            ActionButton_Click(actionButton, EventArgs.Empty);
    }

    private async Task RunActionAsync()
    {
        _isRunning = true;

        try
        {
            var progress = new Progress<ExportProgressInfo>(UpdateProgress);
            UpdateProgress(new ExportProgressInfo(1, "1%   Starting"));

            Result = await Task.Run(() => _action(progress, _cancellationTokenSource.Token));

            UpdateProgress(new ExportProgressInfo(100, "100% Complete"));
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException)
        {
            Result = null;
            progressLabel.Text = "Cleanup cancelled. No uncommitted deletions were applied.";
            DialogResult = DialogResult.Cancel;
        }
        catch (Exception ex)
        {
            Result = null;

            MessageBox.Show(
                "The cleanup action could not be completed.\n\n" + ex.Message,
                _caption,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            DialogResult = DialogResult.Abort;
        }
        finally
        {
            _isRunning = false;
            actionButton.Enabled = true;
            actionButton.Text = "Close";
            ControlBox = true;

            if (DialogResult == DialogResult.OK || DialogResult == DialogResult.Cancel)
                Close();
        }
    }

    private void UpdateProgress(ExportProgressInfo progressInfo)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
            return;

        var percent = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressInfo.Percent));
        progressBar.Value = percent;

        progressLabel.Text = string.IsNullOrWhiteSpace(progressInfo.Message)
            ? percent.ToString(CultureInfo.InvariantCulture) + "%   Working"
            : progressInfo.Message;
    }

}
