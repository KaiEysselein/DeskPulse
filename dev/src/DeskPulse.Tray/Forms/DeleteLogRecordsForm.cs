#nullable enable

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskPulse;

public partial class DeleteLogRecordsForm : Form
{
    public bool CreateRules => createRulesCheckBox.Checked;

    public DeleteLogRecordsForm(int recordCount, string sectionName)
    {
        InitializeComponent();
        AppIcon.Apply(this);

        messageLabel.Text =
            $"Permanently delete {recordCount:N0} selected {sectionName} record(s)?\r\n\r\n" +
            "This action cannot be undone.";

        createRulesCheckBox.Text = recordCount == 1
            ? "Also create an exclusion rule for this record"
            : "Also create exclusion rules for the selected records";
    }
}
