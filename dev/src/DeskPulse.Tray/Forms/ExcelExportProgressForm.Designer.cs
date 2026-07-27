#nullable enable

using System.Windows.Forms;

namespace DeskPulse;

partial class ExcelExportProgressForm
{
    private System.ComponentModel.IContainer? components;
    private Label titleLabel = null!;
    private ProgressBar progressBar = null!;
    private Label percentLabel = null!;
    private Label progressLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        titleLabel = new Label();
        progressBar = new ProgressBar();
        percentLabel = new Label();
        progressLabel = new Label();
        SuspendLayout();
        //
        // titleLabel
        //
        titleLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
        titleLabel.Location = new System.Drawing.Point(24, 22);
        titleLabel.Size = new System.Drawing.Size(500, 30);
        titleLabel.Text = "Exporting log to Excel";
        //
        // progressBar
        //
        progressBar.Location = new System.Drawing.Point(24, 70);
        progressBar.Size = new System.Drawing.Size(432, 24);
        progressBar.Style = ProgressBarStyle.Continuous;
        //
        // percentLabel
        //
        percentLabel.Location = new System.Drawing.Point(464, 70);
        percentLabel.Size = new System.Drawing.Size(60, 24);
        percentLabel.Text = "0%";
        percentLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        //
        // progressLabel
        //
        progressLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        progressLabel.Location = new System.Drawing.Point(24, 108);
        progressLabel.Size = new System.Drawing.Size(500, 42);
        progressLabel.Text = "Waiting to start...";
        //
        // ExcelExportProgressForm
        //
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Window;
        ClientSize = new System.Drawing.Size(548, 166);
        ControlBox = false;
        Controls.Add(titleLabel);
        Controls.Add(progressBar);
        Controls.Add(percentLabel);
        Controls.Add(progressLabel);
        Font = new System.Drawing.Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ExcelExportProgressForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "DeskPulse Excel Export";
        ResumeLayout(false);
    }
}
