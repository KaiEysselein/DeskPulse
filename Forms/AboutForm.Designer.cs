using System.Windows.Forms;

namespace DeskPulse;

partial class AboutForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label titleLabel = null!;
    private Label versionLabel = null!;
    private Label descriptionLabel = null!;
    private Label projectCaptionLabel = null!;
    private LinkLabel projectLinkLabel = null!;
    private Label separatorLabel = null!;
    private Button okButton = null!;

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
        versionLabel = new Label();
        descriptionLabel = new Label();
        projectCaptionLabel = new Label();
        projectLinkLabel = new LinkLabel();
        separatorLabel = new Label();
        okButton = new Button();
        SuspendLayout();
        // 
        // titleLabel
        // 
        titleLabel.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        titleLabel.Location = new System.Drawing.Point(24, 22);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new System.Drawing.Size(380, 30);
        titleLabel.TabIndex = 0;
        // 
        // versionLabel
        // 
        versionLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        versionLabel.Location = new System.Drawing.Point(26, 54);
        versionLabel.Name = "versionLabel";
        versionLabel.Size = new System.Drawing.Size(380, 22);
        versionLabel.TabIndex = 1;
        // 
        // descriptionLabel
        // 
        descriptionLabel.Location = new System.Drawing.Point(26, 92);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new System.Drawing.Size(395, 62);
        descriptionLabel.TabIndex = 2;
        descriptionLabel.Text = "DeskPulse quietly tracks selected file activity while you work. It helps you review what was opened, changed, or saved, and exports clear reports to Excel when needed.";
        // 
        // projectCaptionLabel
        // 
        projectCaptionLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        projectCaptionLabel.Location = new System.Drawing.Point(26, 166);
        projectCaptionLabel.Name = "projectCaptionLabel";
        projectCaptionLabel.Size = new System.Drawing.Size(90, 22);
        projectCaptionLabel.TabIndex = 3;
        projectCaptionLabel.Text = "Project page";
        // 
        // projectLinkLabel
        // 
        projectLinkLabel.Location = new System.Drawing.Point(116, 166);
        projectLinkLabel.Name = "projectLinkLabel";
        projectLinkLabel.Size = new System.Drawing.Size(305, 22);
        projectLinkLabel.TabIndex = 4;
        // 
        // separatorLabel
        // 
        separatorLabel.BorderStyle = BorderStyle.Fixed3D;
        separatorLabel.Location = new System.Drawing.Point(24, 208);
        separatorLabel.Name = "separatorLabel";
        separatorLabel.Size = new System.Drawing.Size(412, 1);
        separatorLabel.TabIndex = 5;
        // 
        // okButton
        // 
        okButton.DialogResult = DialogResult.OK;
        okButton.FlatStyle = FlatStyle.System;
        okButton.Location = new System.Drawing.Point(356, 226);
        okButton.Name = "okButton";
        okButton.Size = new System.Drawing.Size(80, 30);
        okButton.TabIndex = 6;
        okButton.Text = "OK";
        // 
        // AboutForm
        // 
        AcceptButton = okButton;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Window;
        ClientSize = new System.Drawing.Size(460, 270);
        Controls.Add(titleLabel);
        Controls.Add(versionLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(projectCaptionLabel);
        Controls.Add(projectLinkLabel);
        Controls.Add(separatorLabel);
        Controls.Add(okButton);
        Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "AboutForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "About DeskPulse";
        ResumeLayout(false);
    }
}
