#nullable enable

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskPulse;

public partial class DeleteLogRecordsForm : Form
{
    public bool CreateRules => createRulesCheckBox.Checked;
    public string MatchType => matchTypeComboBox.SelectedItem?.ToString() ?? ExactFilePath;

    public const string ExactFilePath = "Exact file path";
    public const string FileNameAnywhere = "File name (any location)";
    public const string FileExtension = "File extension";
    public const string Folder = "Folder (including subfolders)";
    public const string Application = "Application";

    public DeleteLogRecordsForm(
        int recordCount,
        string sectionName,
        LogRuleCategory category,
        bool groupedSelection = false,
        int groupCount = 0)
    {
        InitializeComponent();
        AppIcon.Apply(this);

        var groupLabel = groupCount == 1 ? "group" : "groups";
        messageLabel.Text = groupedSelection
            ? $"Permanently delete {recordCount:N0} records from {groupCount:N0} selected " +
              $"{sectionName} {groupLabel}?\r\n\r\nThis action cannot be undone."
            : $"Permanently delete {recordCount:N0} selected {sectionName} record(s)?\r\n\r\n" +
              "This action cannot be undone.";

        createRulesCheckBox.Text = recordCount == 1
            ? "Also create an exclusion rule for this record"
            : "Also create exclusion rules for the selected records";

        matchTypeComboBox.Items.AddRange(new object[]
        {
            ExactFilePath,
            FileNameAnywhere,
            FileExtension,
            Folder,
            Application
        });
        matchTypeComboBox.SelectedItem = ExactFilePath;
        matchTypePanel.Enabled = category == LogRuleCategory.File;
        createRulesCheckBox.Visible = !groupedSelection;
    }

    private void CreateRulesCheckBox_CheckedChanged(object? sender, EventArgs e) =>
        matchTypePanel.Visible = createRulesCheckBox.Checked && matchTypePanel.Enabled;
}
