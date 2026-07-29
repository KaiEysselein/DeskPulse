#nullable enable

namespace DeskPulse;

partial class DeleteLogRecordsForm
{
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.TableLayoutPanel layout = null!;
    private System.Windows.Forms.Label warningIconLabel = null!;
    private System.Windows.Forms.Label messageLabel = null!;
    private System.Windows.Forms.CheckBox createRulesCheckBox = null!;
    private System.Windows.Forms.FlowLayoutPanel matchTypePanel = null!;
    private System.Windows.Forms.Label matchTypeLabel = null!;
    private System.Windows.Forms.ComboBox matchTypeComboBox = null!;
    private System.Windows.Forms.FlowLayoutPanel buttonPanel = null!;
    private System.Windows.Forms.Button deleteButton = null!;
    private System.Windows.Forms.Button cancelButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        warningIconLabel = new System.Windows.Forms.Label();
        messageLabel = new System.Windows.Forms.Label();
        createRulesCheckBox = new System.Windows.Forms.CheckBox();
        matchTypePanel = new System.Windows.Forms.FlowLayoutPanel();
        matchTypeLabel = new System.Windows.Forms.Label();
        matchTypeComboBox = new System.Windows.Forms.ComboBox();
        buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
        deleteButton = new System.Windows.Forms.Button();
        cancelButton = new System.Windows.Forms.Button();
        layout.SuspendLayout();
        matchTypePanel.SuspendLayout();
        buttonPanel.SuspendLayout();
        SuspendLayout();
        //
        // layout
        //
        layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 58F));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.Controls.Add(warningIconLabel, 0, 0);
        layout.Controls.Add(messageLabel, 1, 0);
        layout.Controls.Add(createRulesCheckBox, 1, 1);
        layout.Controls.Add(matchTypePanel, 1, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Location = new System.Drawing.Point(12, 12);
        layout.Name = "layout";
        layout.RowCount = 4;
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.Size = new System.Drawing.Size(496, 211);
        layout.TabIndex = 0;
        //
        // warningIconLabel
        //
        warningIconLabel.AutoSize = true;
        warningIconLabel.Font = new System.Drawing.Font("Segoe UI", 26F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        warningIconLabel.Location = new System.Drawing.Point(3, 0);
        warningIconLabel.Name = "warningIconLabel";
        warningIconLabel.Size = new System.Drawing.Size(51, 47);
        warningIconLabel.TabIndex = 0;
        warningIconLabel.Text = "⚠";
        //
        // messageLabel
        //
        messageLabel.AutoSize = true;
        messageLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        messageLabel.Location = new System.Drawing.Point(61, 0);
        messageLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 8);
        messageLabel.MaximumSize = new System.Drawing.Size(425, 0);
        messageLabel.Name = "messageLabel";
        messageLabel.Size = new System.Drawing.Size(432, 50);
        messageLabel.TabIndex = 1;
        messageLabel.Text = "Delete selected records?";
        //
        // createRulesCheckBox
        //
        createRulesCheckBox.AutoSize = true;
        createRulesCheckBox.Checked = false;
        createRulesCheckBox.CheckState = System.Windows.Forms.CheckState.Unchecked;
        createRulesCheckBox.Location = new System.Drawing.Point(61, 61);
        createRulesCheckBox.Margin = new System.Windows.Forms.Padding(3, 3, 3, 14);
        createRulesCheckBox.Name = "createRulesCheckBox";
        createRulesCheckBox.Size = new System.Drawing.Size(286, 24);
        createRulesCheckBox.TabIndex = 2;
        createRulesCheckBox.Text = "Also create exclusion rule(s)";
        createRulesCheckBox.UseVisualStyleBackColor = true;
        createRulesCheckBox.CheckedChanged += CreateRulesCheckBox_CheckedChanged;
        //
        // matchTypePanel
        //
        matchTypePanel.AutoSize = true;
        matchTypePanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        matchTypePanel.Controls.Add(matchTypeLabel);
        matchTypePanel.Controls.Add(matchTypeComboBox);
        matchTypePanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        matchTypePanel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 12);
        matchTypePanel.Name = "matchTypePanel";
        matchTypePanel.TabIndex = 3;
        matchTypePanel.Visible = false;
        matchTypePanel.WrapContents = false;
        //
        // matchTypeLabel
        //
        matchTypeLabel.AutoSize = true;
        matchTypeLabel.Margin = new System.Windows.Forms.Padding(0, 7, 8, 0);
        matchTypeLabel.Text = "Match by:";
        //
        // matchTypeComboBox
        //
        matchTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        matchTypeComboBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
        matchTypeComboBox.Size = new System.Drawing.Size(240, 23);
        //
        // buttonPanel
        //
        layout.SetColumnSpan(buttonPanel, 2);
        buttonPanel.AutoSize = true;
        buttonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(deleteButton);
        buttonPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        buttonPanel.Location = new System.Drawing.Point(3, 102);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Padding = new System.Windows.Forms.Padding(0, 8, 8, 0);
        buttonPanel.Size = new System.Drawing.Size(490, 46);
        buttonPanel.TabIndex = 4;
        buttonPanel.WrapContents = false;
        //
        // deleteButton
        //
        deleteButton.AutoSize = true;
        deleteButton.DialogResult = System.Windows.Forms.DialogResult.OK;
        deleteButton.Location = new System.Drawing.Point(300, 11);
        deleteButton.Margin = new System.Windows.Forms.Padding(8, 3, 3, 3);
        deleteButton.MinimumSize = new System.Drawing.Size(90, 32);
        deleteButton.Name = "deleteButton";
        deleteButton.Size = new System.Drawing.Size(90, 32);
        deleteButton.TabIndex = 0;
        deleteButton.Text = "Delete";
        deleteButton.UseVisualStyleBackColor = true;
        //
        // cancelButton
        //
        cancelButton.AutoSize = true;
        cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        cancelButton.Location = new System.Drawing.Point(397, 11);
        cancelButton.Margin = new System.Windows.Forms.Padding(3);
        cancelButton.MinimumSize = new System.Drawing.Size(90, 32);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new System.Drawing.Size(90, 32);
        cancelButton.TabIndex = 1;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;
        //
        // DeleteLogRecordsForm
        //
        AcceptButton = deleteButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new System.Drawing.Size(520, 235);
        Controls.Add(layout);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DeleteLogRecordsForm";
        Padding = new System.Windows.Forms.Padding(12);
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "Delete selected log records";
        layout.ResumeLayout(false);
        layout.PerformLayout();
        matchTypePanel.ResumeLayout(false);
        matchTypePanel.PerformLayout();
        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        ResumeLayout(false);
    }
}
