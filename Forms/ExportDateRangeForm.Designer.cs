using System.Windows.Forms;

namespace DeskPulse;

partial class ExportDateRangeForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label titleLabel = null!;
    private Label hintLabel = null!;
    private GroupBox startGroupBox = null!;
    private MonthCalendar startCalendar = null!;
    private GroupBox endGroupBox = null!;
    private MonthCalendar endCalendar = null!;
    private ProgressBar progressBar = null!;
    private Label progressLabel = null!;
    private Label separatorLabel = null!;
    private Button todayOnlyButton = null!;
    private Button exportButton = null!;
    private Button cancelButton = null!;

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
        hintLabel = new Label();
        startGroupBox = new GroupBox();
        startCalendar = new MonthCalendar();
        endGroupBox = new GroupBox();
        endCalendar = new MonthCalendar();
        progressBar = new ProgressBar();
        progressLabel = new Label();
        separatorLabel = new Label();
        todayOnlyButton = new Button();
        exportButton = new Button();
        cancelButton = new Button();
        startGroupBox.SuspendLayout();
        endGroupBox.SuspendLayout();
        SuspendLayout();
        // 
        // titleLabel
        // 
        titleLabel.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        titleLabel.Location = new System.Drawing.Point(24, 22);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new System.Drawing.Size(500, 28);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Export activity log";
        // 
        // hintLabel
        // 
        hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        hintLabel.Location = new System.Drawing.Point(24, 56);
        hintLabel.Name = "hintLabel";
        hintLabel.Size = new System.Drawing.Size(500, 34);
        hintLabel.TabIndex = 1;
        hintLabel.Text = "The start day defaults to the first record in the database and the end day defaults to today.";
        // 
        // startGroupBox
        // 
        startGroupBox.Controls.Add(startCalendar);
        startGroupBox.Location = new System.Drawing.Point(24, 104);
        startGroupBox.Name = "startGroupBox";
        startGroupBox.Size = new System.Drawing.Size(248, 198);
        startGroupBox.TabIndex = 2;
        startGroupBox.TabStop = false;
        startGroupBox.Text = "Start day";
        // 
        // startCalendar
        // 
        startCalendar.Location = new System.Drawing.Point(12, 22);
        startCalendar.MaxSelectionCount = 1;
        startCalendar.Name = "startCalendar";
        startCalendar.ShowTodayCircle = true;
        startCalendar.TabIndex = 0;
        // 
        // endGroupBox
        // 
        endGroupBox.Controls.Add(endCalendar);
        endGroupBox.Location = new System.Drawing.Point(288, 104);
        endGroupBox.Name = "endGroupBox";
        endGroupBox.Size = new System.Drawing.Size(248, 198);
        endGroupBox.TabIndex = 3;
        endGroupBox.TabStop = false;
        endGroupBox.Text = "End day";
        // 
        // endCalendar
        // 
        endCalendar.Location = new System.Drawing.Point(12, 22);
        endCalendar.MaxSelectionCount = 1;
        endCalendar.Name = "endCalendar";
        endCalendar.ShowTodayCircle = true;
        endCalendar.TabIndex = 0;
        // 
        // progressBar
        // 
        progressBar.Location = new System.Drawing.Point(24, 338);
        progressBar.Name = "progressBar";
        progressBar.Size = new System.Drawing.Size(512, 20);
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.TabIndex = 4;
        progressBar.Visible = false;
        // 
        // progressLabel
        // 
        progressLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        progressLabel.Location = new System.Drawing.Point(24, 368);
        progressLabel.Name = "progressLabel";
        progressLabel.Size = new System.Drawing.Size(500, 22);
        progressLabel.TabIndex = 5;
        progressLabel.Text = "0%   Waiting to export";
        progressLabel.Visible = false;
        // 
        // separatorLabel
        // 
        separatorLabel.BorderStyle = BorderStyle.Fixed3D;
        separatorLabel.Location = new System.Drawing.Point(24, 398);
        separatorLabel.Name = "separatorLabel";
        separatorLabel.Size = new System.Drawing.Size(512, 1);
        separatorLabel.TabIndex = 6;
        // 
        // todayOnlyButton
        // 
        todayOnlyButton.FlatStyle = FlatStyle.System;
        todayOnlyButton.Location = new System.Drawing.Point(248, 412);
        todayOnlyButton.Name = "todayOnlyButton";
        todayOnlyButton.Size = new System.Drawing.Size(96, 30);
        todayOnlyButton.TabIndex = 7;
        todayOnlyButton.Text = "Today Only";
        // 
        // exportButton
        // 
        exportButton.FlatStyle = FlatStyle.System;
        exportButton.Location = new System.Drawing.Point(360, 412);
        exportButton.Name = "exportButton";
        exportButton.Size = new System.Drawing.Size(80, 30);
        exportButton.TabIndex = 8;
        exportButton.Text = "Export";
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.FlatStyle = FlatStyle.System;
        cancelButton.Location = new System.Drawing.Point(456, 412);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new System.Drawing.Size(80, 30);
        cancelButton.TabIndex = 9;
        cancelButton.Text = "Cancel";
        // 
        // ExportDateRangeForm
        // 
        AcceptButton = exportButton;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Window;
        CancelButton = cancelButton;
        ClientSize = new System.Drawing.Size(560, 450);
        Controls.Add(titleLabel);
        Controls.Add(hintLabel);
        Controls.Add(startGroupBox);
        Controls.Add(endGroupBox);
        Controls.Add(progressBar);
        Controls.Add(progressLabel);
        Controls.Add(separatorLabel);
        Controls.Add(todayOnlyButton);
        Controls.Add(exportButton);
        Controls.Add(cancelButton);
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ExportDateRangeForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Export Activity Log";
        startGroupBox.ResumeLayout(false);
        endGroupBox.ResumeLayout(false);
        ResumeLayout(false);
    }
}
