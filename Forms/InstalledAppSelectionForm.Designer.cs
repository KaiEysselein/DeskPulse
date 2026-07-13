#nullable enable

namespace DeskPulse;

partial class InstalledAppSelectionForm
{
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.Label _instructionLabel = null!;
    private System.Windows.Forms.Label _searchLabel = null!;
    private System.Windows.Forms.TextBox _searchTextBox = null!;
    private System.Windows.Forms.CheckedListBox _applicationList = null!;
    private System.Windows.Forms.Label _resultCountLabel = null!;
    private System.Windows.Forms.Label _noteLabel = null!;
    private System.Windows.Forms.Button _addSelectedButton = null!;
    private System.Windows.Forms.Button _cancelButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _instructionLabel = new System.Windows.Forms.Label();
        _searchLabel = new System.Windows.Forms.Label();
        _searchTextBox = new System.Windows.Forms.TextBox();
        _applicationList = new System.Windows.Forms.CheckedListBox();
        _resultCountLabel = new System.Windows.Forms.Label();
        _noteLabel = new System.Windows.Forms.Label();
        _addSelectedButton = new System.Windows.Forms.Button();
        _cancelButton = new System.Windows.Forms.Button();
        SuspendLayout();
        // 
        // _instructionLabel
        // 
        _instructionLabel.AutoSize = true;
        _instructionLabel.Location = new System.Drawing.Point(14, 14);
        _instructionLabel.Name = "_instructionLabel";
        _instructionLabel.Size = new System.Drawing.Size(402, 15);
        _instructionLabel.TabIndex = 0;
        _instructionLabel.Text = "Select one or more installed applications to add as App Activity Include rules.";
        // 
        // _searchLabel
        // 
        _searchLabel.AutoSize = true;
        _searchLabel.Location = new System.Drawing.Point(14, 47);
        _searchLabel.Name = "_searchLabel";
        _searchLabel.Size = new System.Drawing.Size(42, 15);
        _searchLabel.TabIndex = 1;
        _searchLabel.Text = "Search";
        // 
        // _searchTextBox
        // 
        _searchTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        _searchTextBox.Location = new System.Drawing.Point(70, 43);
        _searchTextBox.Name = "_searchTextBox";
        _searchTextBox.Size = new System.Drawing.Size(650, 23);
        _searchTextBox.TabIndex = 2;
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;
        // 
        // _applicationList
        // 
        _applicationList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        _applicationList.CheckOnClick = true;
        _applicationList.FormattingEnabled = true;
        _applicationList.HorizontalScrollbar = true;
        _applicationList.IntegralHeight = false;
        _applicationList.Location = new System.Drawing.Point(14, 77);
        _applicationList.Name = "_applicationList";
        _applicationList.Size = new System.Drawing.Size(706, 366);
        _applicationList.TabIndex = 3;
        _applicationList.DoubleClick += ApplicationList_DoubleClick;
        // 
        // _resultCountLabel
        // 
        _resultCountLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        _resultCountLabel.AutoSize = true;
        _resultCountLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _resultCountLabel.Location = new System.Drawing.Point(14, 451);
        _resultCountLabel.Name = "_resultCountLabel";
        _resultCountLabel.Size = new System.Drawing.Size(0, 15);
        _resultCountLabel.TabIndex = 4;
        // 
        // _noteLabel
        // 
        _noteLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        _noteLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _noteLabel.Location = new System.Drawing.Point(14, 474);
        _noteLabel.Name = "_noteLabel";
        _noteLabel.Size = new System.Drawing.Size(706, 34);
        _noteLabel.TabIndex = 5;
        _noteLabel.Text = "Only applications with an executable path available in Windows registration data are shown. Some Microsoft Store or portable apps may not appear.";
        // 
        // _addSelectedButton
        // 
        _addSelectedButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        _addSelectedButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
        _addSelectedButton.Location = new System.Drawing.Point(509, 517);
        _addSelectedButton.Name = "_addSelectedButton";
        _addSelectedButton.Size = new System.Drawing.Size(115, 30);
        _addSelectedButton.TabIndex = 6;
        _addSelectedButton.Text = "Add Selected";
        _addSelectedButton.UseVisualStyleBackColor = true;
        _addSelectedButton.Click += AddSelectedButton_Click;
        // 
        // _cancelButton
        // 
        _cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        _cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        _cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
        _cancelButton.Location = new System.Drawing.Point(630, 517);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Size = new System.Drawing.Size(90, 30);
        _cancelButton.TabIndex = 7;
        _cancelButton.Text = "Cancel";
        _cancelButton.UseVisualStyleBackColor = true;
        // 
        // InstalledAppSelectionForm
        // 
        AcceptButton = _addSelectedButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        CancelButton = _cancelButton;
        ClientSize = new System.Drawing.Size(734, 561);
        Controls.Add(_cancelButton);
        Controls.Add(_addSelectedButton);
        Controls.Add(_noteLabel);
        Controls.Add(_resultCountLabel);
        Controls.Add(_applicationList);
        Controls.Add(_searchTextBox);
        Controls.Add(_searchLabel);
        Controls.Add(_instructionLabel);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new System.Drawing.Size(600, 440);
        Name = "InstalledAppSelectionForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "Add Installed Applications";
        ResumeLayout(false);
        PerformLayout();
    }
}
