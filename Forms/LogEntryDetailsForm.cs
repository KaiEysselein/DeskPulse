#nullable enable

using System.Collections.Generic;
using System.Windows.Forms;

namespace DeskPulse;

public partial class LogEntryDetailsForm : Form
{
    public LogEntryDetailsForm(IReadOnlyDictionary<string, string> fields)
    {
        InitializeComponent();
        foreach (var field in fields)
        {
            var row = detailsGrid.Rows.Add(field.Key, field.Value);
            detailsGrid.Rows[row].Cells[1].Style.WrapMode = DataGridViewTriState.True;
        }
    }

    private void CopyButton_Click(object? sender, System.EventArgs e)
    {
        var lines = new List<string>();
        foreach (DataGridViewRow row in detailsGrid.Rows)
            lines.Add($"{row.Cells[0].Value}: {row.Cells[1].Value}");
        Clipboard.SetText(string.Join(System.Environment.NewLine, lines));
    }
}
