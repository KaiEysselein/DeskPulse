using System.Windows.Forms;

namespace DeskPulse;

partial class MaintenanceProgressForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label titleLabel = null!;
    private ProgressBar progressBar = null!;
    private Label progressLabel = null!;
    private Button closeButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        titleLabel = new Label();
        progressBar = new ProgressBar();
        progressLabel = new Label();
        closeButton = new Button();
        SuspendLayout();
        // 
        // titleLabel
        // 
        titleLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        titleLabel.Location = new System.Drawing.Point(22, 20);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new System.Drawing.Size(470, 28);
        titleLabel.TabIndex = 0;
        // 
        // progressBar
        // 
        progressBar.Location = new System.Drawing.Point(22, 66);
        progressBar.Name = "progressBar";
        progressBar.Size = new System.Drawing.Size(476, 20);
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.TabIndex = 1;
        // 
        // progressLabel
        // 
        progressLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        progressLabel.Location = new System.Drawing.Point(22, 96);
        progressLabel.Name = "progressLabel";
        progressLabel.Size = new System.Drawing.Size(476, 34);
        progressLabel.TabIndex = 2;
        progressLabel.Text = "0%   Waiting to start";
        // 
        // closeButton
        // 
        closeButton.Enabled = false;
        closeButton.FlatStyle = FlatStyle.System;
        closeButton.Location = new System.Drawing.Point(418, 140);
        closeButton.Name = "closeButton";
        closeButton.Size = new System.Drawing.Size(80, 30);
        closeButton.TabIndex = 3;
        closeButton.Text = "Close";
        // 
        // MaintenanceProgressForm
        // 
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Window;
        ClientSize = new System.Drawing.Size(520, 184);
        ControlBox = false;
        Controls.Add(titleLabel);
        Controls.Add(progressBar);
        Controls.Add(progressLabel);
        Controls.Add(closeButton);
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "MaintenanceProgressForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "DeskPulse Maintenance";
        ResumeLayout(false);
    }
}
