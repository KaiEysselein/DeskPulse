#nullable enable

using System;
using System.Windows.Forms;

namespace DeskPulse;

public sealed class AddLogRuleForm : Form
{
    private readonly CheckBox _enabled = new() { Text = "On", Checked = true, AutoSize = true };
    private readonly RadioButton _include = new() { Text = "Include", AutoSize = true };
    private readonly RadioButton _exclude = new() { Text = "Exclude", Checked = true, AutoSize = true };
    private readonly TextBox _value = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _cleanup = new() { Text = "Clean up old data, removing records in conflict with this rule", AutoSize = true };

    public AddLogRuleForm(
        LogRuleCategory category,
        string suggestedValue,
        string ruleType,
        bool exclusionOnly = false)
    {
        Category = category;
        AppIcon.Apply(this);
        Text = "Create rule";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new System.Drawing.Size(620, category == LogRuleCategory.User ? 205 : 235);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = category == LogRuleCategory.User ? 4 : 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var valueLabel = new Label
        {
            AutoSize = true,
            Text = category switch
            {
                LogRuleCategory.App when exclusionOnly => "Application:",
                LogRuleCategory.App => "App / pattern:",
                LogRuleCategory.File when ruleType.Equals("folder", StringComparison.OrdinalIgnoreCase) => "Folder:",
                LogRuleCategory.File => "File / pattern:",
                _ => "Event / text:"
            },
            Anchor = AnchorStyles.Left
        };

        _value.Text = suggestedValue;
        if (exclusionOnly)
        {
            _include.Checked = false;
            _include.Enabled = false;
            _exclude.Checked = true;
        }
        _include.CheckedChanged += (_, _) =>
        {
            if (_include.Checked)
                _cleanup.Checked = false;
            _cleanup.Enabled = !_include.Checked;
        };
        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        actionPanel.Controls.Add(_enabled);
        actionPanel.Controls.Add(_include);
        actionPanel.Controls.Add(_exclude);

        layout.Controls.Add(new Label { Text = "Rule:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(actionPanel, 1, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        layout.Controls.Add(_value, 1, 1);

        var nextRow = 2;
        if (category != LogRuleCategory.User)
        {
            var ruleHint = new Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.SystemColors.GrayText,
                Text = exclusionOnly
                    ? "Exact process name used by File Activity (for example, icacls or OneDrive.exe).\r\nWildcards are not supported; matching is case-insensitive."
                    : ruleType.Equals("folder", StringComparison.OrdinalIgnoreCase)
                        ? "Matches this folder and all of its subfolders."
                        : category == LogRuleCategory.App
                            ? @"App names may use * and ?. Full-path patterns may also use ** for subfolders." +
                              "\r\n" + @"Example: C:\Tools\**\*.exe"
                            : "Use an exact filename or a wildcard pattern such as *.pdf."
            };
            layout.Controls.Add(ruleHint, 1, nextRow);
            nextRow++;
        }

        layout.Controls.Add(new Label { Text = "Existing data:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, nextRow);
        layout.Controls.Add(_cleanup, 1, nextRow);
        nextRow++;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "OK", AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_value.Text))
            {
                MessageBox.Show(this, "Enter a rule value.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(buttons, 0, nextRow);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    public LogRuleCategory Category { get; }
    public bool RuleEnabled => _enabled.Checked;
    public bool IsInclude => _include.Checked;
    public string RuleValue => _value.Text.Trim();
    public bool CleanUpOldData => _cleanup.Checked;
}

public enum LogRuleCategory
{
    App,
    File,
    User
}
