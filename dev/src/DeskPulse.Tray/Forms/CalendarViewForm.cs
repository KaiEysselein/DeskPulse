#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace DeskPulse;

public sealed class CalendarViewForm : Form
{
    private readonly string _connectionString;
    private readonly MonthCalendar _calendar = new() { MaxSelectionCount = 1, ShowTodayCircle = true };
    private readonly CheckBox _showAll = new() { Text = "All dates", Checked = true, AutoSize = true };
    private readonly Label _status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TabControl _activityTabs = new();
    private readonly DataGridView _grid = new();
    private readonly bool _use12HourTime;
    private List<CalendarEntry> _entries = new();
    private DateTime? _selectedDateFilter;
    private readonly Dictionary<string, string> _groupByByActivityType = new(StringComparer.Ordinal)
    {
        ["File Activity"] = "None",
        ["App Activity"] = "None",
        ["User Activity"] = "None"
    };
    private readonly Dictionary<string, HashSet<string>> _expandedGroupsByActivityType = new(StringComparer.Ordinal)
    {
        ["File Activity"] = new(StringComparer.Ordinal),
        ["App Activity"] = new(StringComparer.Ordinal),
        ["User Activity"] = new(StringComparer.Ordinal)
    };
    private readonly Font _groupFont;
    private int _viewProgressDepth;
    private bool _updatingGrid;

    public event Action<DateTime?>? DateFilterChanged;
    public DateTime? SelectedDateFilter => _selectedDateFilter;

    public CalendarViewForm(
        string databaseFilePath,
        string windowTitle = "DeskPulse - Calendar View",
        bool use12HourTime = false)
    {
        _use12HourTime = use12HourTime;
        _groupFont = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Text = windowTitle;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 620);
        MinimumSize = new Size(760, 480);
        WindowState = FormWindowState.Maximized;
        AppIcon.Apply(this);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(12, 8, 12, 4)
        };
        var headerText = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 6, 0, 0),
            Text = "Double-click Date, Time, Activity, Item, or Details to group or ungroup."
        };
        header.Controls.Add(headerText);

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(12, 8, 8, 12) };
        _calendar.Dock = DockStyle.Top;
        _showAll.Top = 190;
        _showAll.Left = 12;
        _status.Top = 222;
        _status.Left = 12;
        _status.Width = 230;
        _status.Height = 72;
        leftPanel.Controls.Add(_calendar);
        leftPanel.Controls.Add(_showAll);
        leftPanel.Controls.Add(_status);

        ConfigureGrid();
        ConfigureActivityTabs();
        Controls.Add(_activityTabs);
        Controls.Add(leftPanel);
        Controls.Add(header);

        _calendar.DateSelected += (_, e) =>
        {
            _showAll.Checked = false;
            ShowEntries(e.Start.Date);
        };
        _showAll.CheckedChanged += (_, _) =>
        {
            if (_showAll.Checked)
                ShowEntries(null);
            else
                ShowEntries(_calendar.SelectionStart.Date);
        };
        Activated += (_, _) => LoadCalendarEntries();
        FormClosed += (_, _) => _groupFont.Dispose();
    }

    public void RefreshCalendar() => LoadCalendarEntries();

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToOrderColumns = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActivityType", HeaderText = "Activity", FillWeight = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Item", FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", HeaderText = "Details", FillWeight = 210 });
        _grid.ColumnHeaderMouseDoubleClick += Grid_ColumnHeaderMouseDoubleClick;
        _grid.CellDoubleClick += Grid_CellDoubleClick;
    }

    private void ConfigureActivityTabs()
    {
        _activityTabs.Dock = DockStyle.Fill;
        AddActivityTab("Files", "File Activity");
        AddActivityTab("Apps", "App Activity");
        AddActivityTab("User Activity", "User Activity");
        _activityTabs.SelectedIndexChanged += (_, _) =>
        {
            var selectedPage = _activityTabs.SelectedTab;
            if (selectedPage == null)
                return;

            selectedPage.Controls.Add(_grid);
            _grid.Dock = DockStyle.Fill;
            ShowEntriesCore(_selectedDateFilter);
        };
        _activityTabs.TabPages[0].Controls.Add(_grid);
    }

    private void AddActivityTab(string title, string activityType) =>
        _activityTabs.TabPages.Add(new TabPage(title) { Tag = activityType });

    private void LoadCalendarEntries()
    {
        RunWithViewProgress(
            "Loading selected Calendar records...",
            LoadCalendarEntriesCore);
    }

    private void LoadCalendarEntriesCore()
    {
        try
        {
            _entries.Clear();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = BuildEntriesQuery(markedRecordsOnly: true);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var createdAtText = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (!DateTime.TryParse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var createdAt) &&
                    !DateTime.TryParse(createdAtText, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out createdAt))
                {
                    continue;
                }

                _entries.Add(new CalendarEntry(
                    createdAt,
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3)));
            }

            var markedDates = _entries.Select(entry => entry.CreatedAt.Date).Distinct().ToArray();
            _calendar.BoldedDates = markedDates;
            if (_entries.Count > 0)
                _calendar.SetDate(_entries[0].CreatedAt.Date);
            ShowEntriesCore(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "DeskPulse could not open Calendar View.\n\n" + ex.Message,
                "Calendar View",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowEntries(DateTime? selectedDate)
    {
        RunWithViewProgress("Updating the Calendar records...", () => ShowEntriesCore(selectedDate));
    }

    private void ShowEntriesCore(DateTime? selectedDate)
    {
        if (_updatingGrid)
            return;

        _updatingGrid = true;
        try
        {
            _selectedDateFilter = selectedDate;
            DateFilterChanged?.Invoke(selectedDate);
            var activityType = SelectedActivityType;
            var groupBy = _groupByByActivityType[activityType];
            var expandedGroups = _expandedGroupsByActivityType[activityType];
            var activityEntries = _entries.Where(entry => entry.ActivityType == activityType);
            var visibleEntries = selectedDate.HasValue
                ? activityEntries.Where(entry => entry.CreatedAt.Date == selectedDate.Value.Date)
                : activityEntries;

            _grid.Rows.Clear();
            var count = 0;
            var entries = visibleEntries.ToList();
            if (groupBy == "None")
            {
                foreach (var entry in entries)
                {
                    AddEntryRow(entry);
                    count++;
                }
            }
            else
            {
                foreach (var group in entries.GroupBy(entry => GetGroupKey(entry, groupBy)))
                {
                    var expanded = expandedGroups.Contains(group.Key);
                    var values = new object[] { "", "", "", "", $"{group.Count():N0} record(s)" };
                    var groupColumn = groupBy switch
                    {
                        "Date" => 0,
                        "Hour" => 1,
                        "Activity" => 2,
                        "Item" => 3,
                        _ => 4
                    };
                    values[groupColumn] = $"{(expanded ? "▼" : "▶")} {group.Key}";
                    var rowIndex = _grid.Rows.Add(values);
                    _grid.Rows[rowIndex].Tag = new CalendarGroup(group.Key);
                    _grid.Rows[rowIndex].DefaultCellStyle.Font = _groupFont;
                    if (expanded)
                    {
                        foreach (var entry in group)
                            AddEntryRow(entry);
                    }
                    count += group.Count();
                }
            }

            var recordDescription = activityType switch
            {
                "File Activity" => "selected file record(s)",
                "App Activity" => "selected app record(s)",
                _ => "selected user activity record(s)"
            };
            _status.Text = selectedDate.HasValue
                ? $"{count:N0} {recordDescription} on {selectedDate.Value:dd MMMM yyyy}."
                : $"{count:N0} {recordDescription} across all dates.\r\nBold dates contain selected Calendar records.";
            _grid.ClearSelection();
        }
        finally
        {
            _updatingGrid = false;
        }
    }

    private string SelectedActivityType =>
        _activityTabs.SelectedTab?.Tag as string ?? "File Activity";

    private void RunWithViewProgress(string message, Action operation)
    {
        if (_viewProgressDepth > 0 || !IsHandleCreated || !Visible)
        {
            operation();
            return;
        }

        _viewProgressDepth++;
        using var progress = ViewProgressSession.Start(message);
        try
        {
            operation();
        }
        finally
        {
            _viewProgressDepth--;
        }
    }

    private void AddEntryRow(CalendarEntry entry)
    {
        var rowIndex = _grid.Rows.Add(
            entry.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            entry.CreatedAt.ToString(_use12HourTime ? "hh:mm:ss tt" : "HH:mm:ss", CultureInfo.InvariantCulture),
            entry.ActivityType,
            entry.Subject,
            entry.Details);
        _grid.Rows[rowIndex].Tag = entry;
    }

    private string GetGroupKey(CalendarEntry entry, string groupBy) => groupBy switch
    {
        "Date" => entry.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "Hour" => entry.CreatedAt.ToString(_use12HourTime ? "hh:00 tt" : "HH:00", CultureInfo.InvariantCulture),
        "Activity" => entry.ActivityType,
        "Item" => string.IsNullOrWhiteSpace(entry.Subject) ? "(no item)" : entry.Subject,
        "Details" => string.IsNullOrWhiteSpace(entry.Details) ? "(no details)" : entry.Details,
        _ => ""
    };

    private void Grid_ColumnHeaderMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0)
            return;
        var grouping = GetHeaderGrouping(_grid.Columns[e.ColumnIndex].Name);
        if (grouping == null)
            return;
        var activityType = SelectedActivityType;
        ToggleTabGrouping(_groupByByActivityType, activityType, grouping);
        _expandedGroupsByActivityType[activityType].Clear();
        ShowEntriesCore(_selectedDateFilter);
    }

    public static string? GetHeaderGrouping(string columnName) => columnName switch
    {
        "Date" => "Date",
        "Time" => "Hour",
        "ActivityType" => "Activity",
        "Subject" => "Item",
        "Details" => "Details",
        _ => null
    };

    public static string ToggleHeaderGrouping(string currentGrouping, string clickedGrouping) =>
        currentGrouping == clickedGrouping ? "None" : clickedGrouping;

    public static string ToggleTabGrouping(
        IDictionary<string, string> groupings,
        string activityType,
        string clickedGrouping)
    {
        var currentGrouping = groupings.TryGetValue(activityType, out var value) ? value : "None";
        var updatedGrouping = ToggleHeaderGrouping(currentGrouping, clickedGrouping);
        groupings[activityType] = updatedGrouping;
        return updatedGrouping;
    }

    public static string BuildEntriesQuery(bool markedRecordsOnly)
    {
        var filter = markedRecordsOnly ? "WHERE ShowInCalendarView <> 0" : "";
        return
            $"""
            SELECT CreatedAt,
                   'File Activity' AS ActivityType,
                   COALESCE(NULLIF(FileName, ''), NULLIF(FullPath, ''), NULLIF(Item, ''), '(no item)') AS Subject,
                   COALESCE(NULLIF(FullPath, ''), NULLIF(Note, ''), '') AS Details
            FROM ActivityEvents
            {filter}
            UNION ALL
            SELECT CreatedAt,
                   'App Activity' AS ActivityType,
                   COALESCE(NULLIF(ProgramName, ''), NULLIF(EventDescription, ''), '(no app)') AS Subject,
                   COALESCE(NULLIF(WindowTitle, ''), NULLIF(FilePath, ''), NULLIF(EventDescription, ''), '') AS Details
            FROM ProgramEvents
            {filter}
            UNION ALL
            SELECT CreatedAt,
                   'User Activity' AS ActivityType,
                   COALESCE(NULLIF(EventDescription, ''), '(no event)') AS Subject,
                   COALESCE(NULLIF(UserName, ''), NULLIF(Note, ''), '') AS Details
            FROM UserEvents
            {filter}
            ORDER BY CreatedAt DESC;
            """;
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not CalendarGroup group)
            return;
        var expandedGroups = _expandedGroupsByActivityType[SelectedActivityType];
        if (!expandedGroups.Add(group.Key))
            expandedGroups.Remove(group.Key);
        ShowEntriesCore(_selectedDateFilter);
    }

    public int ExportDisplayedRecords(string fileName)
    {
        var activityEntries = _entries.Where(entry => entry.ActivityType == SelectedActivityType);
        var visibleEntries = (_selectedDateFilter.HasValue
            ? activityEntries.Where(entry => entry.CreatedAt.Date == _selectedDateFilter.Value.Date)
            : activityEntries).ToList();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Calendar");
        var headers = new[] { "Date", "Time", "Activity", "Item", "Details" };
        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
            worksheet.Cell(1, index + 1).Style.Font.Bold = true;
        }
        for (var index = 0; index < visibleEntries.Count; index++)
        {
            var entry = visibleEntries[index];
            worksheet.Cell(index + 2, 1).Value = entry.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            worksheet.Cell(index + 2, 2).Value = entry.CreatedAt.ToString(_use12HourTime ? "hh:mm:ss tt" : "HH:mm:ss", CultureInfo.InvariantCulture);
            worksheet.Cell(index + 2, 3).Value = entry.ActivityType;
            worksheet.Cell(index + 2, 4).Value = entry.Subject;
            worksheet.Cell(index + 2, 5).Value = entry.Details;
        }
        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.Columns().AdjustToContents(8, 60);
        workbook.SaveAs(fileName);
        return visibleEntries.Count;
    }

    private sealed record CalendarEntry(DateTime CreatedAt, string ActivityType, string Subject, string Details);
    private sealed record CalendarGroup(string Key);
}
