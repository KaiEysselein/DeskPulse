using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DeskPulse;

public sealed class FilteredFileActivityProcessesForm : Form
{
    private readonly List<FileActivityProcessSummary> _summaries;
    private readonly TextBox _searchBox = new();
    private readonly ListBox _availableList = new();
    private readonly ListBox _filteredList = new();
    private readonly Button _addButton = new();
    private readonly Button _removeButton = new();
    private readonly Button _manualButton = new();
    private readonly Button _clearButton = new();

    public FilteredFileActivityProcessesForm(IEnumerable<FileActivityProcessSummary> summaries, IEnumerable<string> filteredProcesses)
    {
        _summaries = (summaries ?? Array.Empty<FileActivityProcessSummary>())
            .OrderBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();

        Text = "Filtered File Activity Applications";
        Width = 900;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AppIcon.Apply(this);

        Controls.Add(new Label { Text = "Available applications from existing File Activity data", Left = 18, Top = 18, Width = 350 });
        Controls.Add(new Label { Text = "Filtered applications", Left = 510, Top = 18, Width = 250 });
        Controls.Add(new Label { Text = "Search:", Left = 18, Top = 48, Width = 55 });
        _searchBox.SetBounds(76, 44, 350, 27);
        _searchBox.TextChanged += (_, _) => RefreshLists();
        Controls.Add(_searchBox);

        _availableList.SetBounds(18, 80, 408, 360);
        _availableList.SelectionMode = SelectionMode.MultiExtended;
        _availableList.DoubleClick += (_, _) => AddSelected();
        Controls.Add(_availableList);

        _filteredList.SetBounds(510, 80, 350, 360);
        _filteredList.SelectionMode = SelectionMode.MultiExtended;
        _filteredList.DoubleClick += (_, _) => RemoveSelected();
        Controls.Add(_filteredList);

        _addButton.Text = "Add →";
        _addButton.SetBounds(438, 155, 60, 32);
        _addButton.Click += (_, _) => AddSelected();
        Controls.Add(_addButton);

        _removeButton.Text = "← Remove";
        _removeButton.SetBounds(430, 200, 72, 32);
        _removeButton.Click += (_, _) => RemoveSelected();
        Controls.Add(_removeButton);

        _manualButton.Text = "Add manually...";
        _manualButton.SetBounds(510, 450, 125, 30);
        _manualButton.Click += (_, _) => AddManually();
        Controls.Add(_manualButton);

        _clearButton.Text = "Clear all";
        _clearButton.SetBounds(646, 450, 90, 30);
        _clearButton.Click += (_, _) => { _filtered.Clear(); RefreshLists(); };
        Controls.Add(_clearButton);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        ok.SetBounds(674, 490, 88, 30);
        Controls.Add(ok);
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(772, 490, 88, 30);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        foreach (var process in filteredProcesses ?? Array.Empty<string>())
        {
            var normalized = AppSettings.NormalizeProcessName(process);
            if (!string.IsNullOrWhiteSpace(normalized))
                _filtered.Add(normalized);
        }
        RefreshLists();
    }

    private readonly HashSet<string> _filtered = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> FilteredProcesses => _filtered.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    private void RefreshLists()
    {
        var search = _searchBox.Text.Trim();
        _availableList.BeginUpdate();
        _availableList.Items.Clear();
        foreach (var item in _summaries.Where(item => !_filtered.Contains(item.ProcessName))
                     .Where(item => string.IsNullOrWhiteSpace(search) || item.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase)))
            _availableList.Items.Add(new ProcessListItem(item.ProcessName, item.RecordCount));
        _availableList.EndUpdate();

        _filteredList.BeginUpdate();
        _filteredList.Items.Clear();
        foreach (var process in _filtered.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var count = _summaries.FirstOrDefault(x => x.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase))?.RecordCount;
            _filteredList.Items.Add(new ProcessListItem(process, count));
        }
        _filteredList.EndUpdate();
    }

    private void AddSelected()
    {
        foreach (var item in _availableList.SelectedItems.Cast<ProcessListItem>().ToList())
            _filtered.Add(item.ProcessName);
        RefreshLists();
    }

    private void RemoveSelected()
    {
        foreach (var item in _filteredList.SelectedItems.Cast<ProcessListItem>().ToList())
            _filtered.Remove(item.ProcessName);
        RefreshLists();
    }

    private void AddManually()
    {
        using var prompt = new ProcessNamePromptForm();
        if (prompt.ShowDialog(this) != DialogResult.OK)
            return;
        var normalized = AppSettings.NormalizeProcessName(prompt.ProcessName);
        if (string.IsNullOrWhiteSpace(normalized))
            return;
        _filtered.Add(normalized);
        RefreshLists();
    }

    private sealed class ProcessListItem
    {
        public ProcessListItem(string processName, long? recordCount) { ProcessName = processName; RecordCount = recordCount; }
        public string ProcessName { get; }
        public long? RecordCount { get; }
        public override string ToString() => RecordCount.HasValue
            ? ProcessName + " — " + RecordCount.Value.ToString("N0", CultureInfo.InvariantCulture) + " records"
            : ProcessName;
    }

    private sealed class ProcessNamePromptForm : Form
    {
        private readonly TextBox _textBox = new();
        public string ProcessName => _textBox.Text.Trim();

        public ProcessNamePromptForm()
        {
            Text = "Add Filtered Application";
            Width = 430;
            Height = 165;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            Controls.Add(new Label { Text = "Process name, for example OneDrive.exe:", Left = 14, Top = 16, Width = 360 });
            _textBox.SetBounds(14, 42, 385, 27);
            Controls.Add(_textBox);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            ok.SetBounds(214, 82, 88, 30);
            Controls.Add(ok);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(311, 82, 88, 30);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

}
