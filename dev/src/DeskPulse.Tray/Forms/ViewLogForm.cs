#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace DeskPulse;

public partial class ViewLogForm : Form
{
    private const int DefaultPageSize = 500;
    private const int MaximumPageSize = 10000;
    private const string ViewSettingsRegistryPath = @"Software\DeskPulse";
    private int _pageSize = DefaultPageSize;
    private string _fileGroupBy = "None";
    private string _appGroupBy = "None";
    private bool _use12HourTime;
    private bool _applyingPeriod;
    private readonly HashSet<string> _expandedFileGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedAppGroups = new(StringComparer.Ordinal);
    private bool _updatingGroupByCombo;
    private bool _updatingCalendarCells;
    private readonly System.Windows.Forms.Timer _headerClickTimer = new()
    {
        Interval = SystemInformation.DoubleClickTime
    };
    private DataGridView? _pendingHeaderGrid;
    private int _pendingHeaderColumnIndex = -1;
    private ContextMenuStrip? _reportContextMenu;

    private int _appPage;
    private int _filePage;
    private int _userPage;
    private int _appTotal;
    private int _fileTotal;
    private int _userTotal;
    private readonly string _connectionString;
    private readonly string _databaseFilePath;
    private readonly bool _systemOnly;
    private readonly Action? _settingsChanged;
    private string _appSortColumn = "CreatedAt";
    private bool _appSortAscending;
    private string _appGroupSortColumn = "Latest";
    private bool _appGroupSortAscending;
    private string _fileSortColumn = "CreatedAt";
    private bool _fileSortAscending;
    private string _fileGroupSortColumn = "Latest";
    private bool _fileGroupSortAscending;
    private string _userSortColumn = "CreatedAt";
    private bool _userSortAscending;

    public ViewLogForm(
        Action? settingsChanged = null,
        bool systemOnly = false)
    {
        InitializeComponent();
        AppIcon.Apply(this);

        _settingsChanged = settingsChanged;
        _systemOnly = systemOnly;
        var settings = AppSettings.Load();
        _databaseFilePath = systemOnly
            ? StorageLayout.SystemDatabaseFilePath
            : settings.DatabaseFilePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        if (_systemOnly)
        {
            Text = "DeskPulse - System Log";
            createRuleButton.Visible = false;
            deleteButton.Visible = false;
        }

        _pageSize = LoadPageSize();
        _use12HourTime = LoadUse12HourTime();
        timeFormatCombo.SelectedItem = _use12HourTime ? "12-hour" : "24-hour";
        pageSizeInput.Value = Math.Clamp(_pageSize, (int)pageSizeInput.Minimum, (int)pageSizeInput.Maximum);
        dateStart.Value = GetFirstRecordedDate();
        dateEnd.Value = DateTime.Now;
        periodCombo.Items.AddRange(new object[]
        {
            "Custom",
            "All time",
            "Today (since midnight)",
            "Last 24 hours",
            "Last 7 days",
            "Last 14 days",
            "Last 30 days",
            "This month",
            "Last 60 days",
            "Last 90 days",
            "Last 180 days",
            "Last 1 year",
            "Last 2 years",
            "Last 3 years",
            "Last 4 years",
            "Last 5 years",
            "Last 10 years"
        });
        _applyingPeriod = true;
        try
        {
            periodCombo.SelectedItem = "All time";
        }
        finally
        {
            _applyingPeriod = false;
        }
        dateStart.ValueChanged += (_, _) => MarkPeriodCustom();
        dateEnd.ValueChanged += (_, _) => MarkPeriodCustom();
        ConfigureGrids();
        _headerClickTimer.Tick += (_, _) => ApplyPendingHeaderSort();
        tabs.SelectedIndexChanged += (_, _) =>
        {
            UpdateSelectionButtons();
            UpdatePagingControls();
            UpdateGroupByControls();
            UpdatePageStatus();
        };
        Load += (_, _) =>
        {
            UpdateGroupByControls();
            RefreshLog();
        };
        FormClosed += (_, _) =>
        {
            _reportContextMenu?.Dispose();
            _headerClickTimer.Dispose();
        };
    }

    private SqliteConnection OpenReadConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private DateTime GetFirstRecordedDate()
    {
        return DatabaseDateRange.GetFirstRecordedDate(_databaseFilePath);
    }

    private void PeriodCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_applyingPeriod || periodCombo.SelectedItem?.ToString() is not { } period || period == "Custom")
            return;
        var range = CalculatePeriodRange(period, DateTime.Now, GetFirstRecordedDate());
        _applyingPeriod = true;
        try
        {
            dateStart.Value = ClampPickerValue(dateStart, range.Start);
            dateEnd.Value = ClampPickerValue(dateEnd, range.End);
        }
        finally
        {
            _applyingPeriod = false;
        }
        _filePage = 0;
        _appPage = 0;
        _userPage = 0;
        RefreshLog();
    }

    private void MarkPeriodCustom()
    {
        if (_applyingPeriod || periodCombo.SelectedItem?.ToString() == "Custom")
            return;
        _applyingPeriod = true;
        try { periodCombo.SelectedItem = "Custom"; }
        finally { _applyingPeriod = false; }
    }

    private static DateTime ClampPickerValue(DateTimePicker picker, DateTime value) =>
        value < picker.MinDate ? picker.MinDate : value > picker.MaxDate ? picker.MaxDate : value;

    public static (DateTime Start, DateTime End) CalculatePeriodRange(
        string period,
        DateTime now,
        DateTime? firstRecordedDate = null)
    {
        var start = period switch
        {
            "All time" => firstRecordedDate ?? DateTimePicker.MinimumDateTime,
            "Today (since midnight)" => now.Date,
            "Last 24 hours" => now.AddHours(-24),
            "Last 7 days" => now.AddDays(-7),
            "Last 14 days" => now.AddDays(-14),
            "Last 30 days" => now.AddDays(-30),
            "This month" => new DateTime(now.Year, now.Month, 1),
            "Last 60 days" => now.AddDays(-60),
            "Last 90 days" => now.AddDays(-90),
            "Last 180 days" => now.AddDays(-180),
            "Last 1 year" => now.AddYears(-1),
            "Last 2 years" => now.AddYears(-2),
            "Last 3 years" => now.AddYears(-3),
            "Last 4 years" => now.AddYears(-4),
            "Last 5 years" => now.AddYears(-5),
            "Last 10 years" => now.AddYears(-10),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown log period.")
        };
        return (start, now);
    }

    private void ConfigureGrids()
    {
        ConfigureGrid(gridApp, new[] { "ID", "Date", "Time", "App", "Process ID", "Path" });
        ConfigureGrid(gridFile, new[] { "ID", "Date", "Time", "File", "Extension", "Activity", "Folder", "App" });
        ConfigureGrid(gridUser, new[] { "ID", "Date", "Time", "Event", "User", "Computer" });
    }

    private void ConfigureGrid(DataGridView grid, IReadOnlyList<string> columns)
    {
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToOrderColumns = true;
        grid.ReadOnly = false;
        grid.MultiSelect = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        foreach (var columnName in columns)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = columnName.Replace(" ", ""),
                HeaderText = columnName,
                Tag = columnName,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                FillWeight = columnName is "Folder" or "Path" ? 180 : columnName is "File" or "App" or "Event" ? 130 : columnName == "Extension" ? 65 : columnName == "ID" ? 55 : 80,
                ReadOnly = true
            });
        }

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "ShowInCalendarView",
            HeaderText = "Calendar",
            ToolTipText = "Include this record in Calendar View. On a grouped row, this changes every record in the group.",
            ThreeState = true,
            TrueValue = CheckState.Checked,
            FalseValue = CheckState.Unchecked,
            IndeterminateValue = CheckState.Indeterminate,
            Width = 68,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = false
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RecordCount",
            HeaderText = "Records",
            Tag = "Records",
            SortMode = DataGridViewColumnSortMode.Programmatic,
            FillWeight = 75,
            Visible = false,
            ReadOnly = true
        });

        grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Details",
            HeaderText = "",
            Text = "Details",
            UseColumnTextForButtonValue = true,
            Width = 72,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
            ,ReadOnly = true
        });
        grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Summary",
            HeaderText = "",
            UseColumnTextForButtonValue = false,
            Width = 78,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
            ,ReadOnly = true
        });

        grid.CellContentClick += Grid_CellContentClick;
        grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
        grid.CellValueChanged += Grid_CellValueChanged;
        grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        grid.ColumnHeaderMouseDoubleClick += Grid_ColumnHeaderMouseDoubleClick;
        grid.CellDoubleClick += Grid_CellDoubleClick;
        grid.CellMouseDown += Grid_CellMouseDown;
        grid.SelectionChanged += (_, _) => UpdateSelectionButtons();
    }

    private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_systemOnly ||
            e.Button != MouseButtons.Right ||
            sender is not DataGridView grid ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0)
        {
            return;
        }

        grid.ClearSelection();
        grid.Rows[e.RowIndex].Selected = true;
        grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

        var row = grid.Rows[e.RowIndex];
        var columnName = grid.Columns[e.ColumnIndex].Name;
        _reportContextMenu?.Dispose();
        var menu = new ContextMenuStrip();
        _reportContextMenu = menu;
        var groupedRow = row.Tag is FileLogGroup or AppLogGroup;
        var deleteItem = new ToolStripMenuItem(groupedRow ? "Delete group..." : "Delete record...");
        deleteItem.Click += async (_, _) => await DeleteSelectedAsync(allowRuleCreation: false);
        menu.Items.Add(deleteItem);

        RuleSuggestion suggestion = default;
        var canCreateRule = row.Tag is LogViewEntry entry
            ? TryBuildCellRuleSuggestion(GetGridCategory(grid), columnName, entry, out suggestion)
            : row.Tag is FileLogGroup fileGroup
                ? TryBuildGroupCellRuleSuggestion(
                    LogRuleCategory.File,
                    FileGroupDisplayColumnName(),
                    fileGroup.Key,
                    out suggestion)
                : row.Tag is AppLogGroup appGroup &&
                  TryBuildGroupCellRuleSuggestion(
                      LogRuleCategory.App,
                      AppGroupDisplayColumnName(),
                      appGroup.Key,
                      out suggestion);

        var createRuleItem = new ToolStripMenuItem("Create rule...")
        {
            Enabled = canCreateRule
        };
        if (canCreateRule)
            createRuleItem.Click += async (_, _) => await CreateRuleAsync(suggestion);
        menu.Items.Add(createRuleItem);
        menu.Show(Cursor.Position);
    }

    private LogRuleCategory GetGridCategory(DataGridView grid) =>
        grid == gridApp
            ? LogRuleCategory.App
            : grid == gridUser
                ? LogRuleCategory.User
                : LogRuleCategory.File;

    public static bool TryBuildCellRuleSuggestion(
        LogRuleCategory reportCategory,
        string columnName,
        LogViewEntry entry,
        out RuleSuggestion suggestion)
    {
        suggestion = default;
        var value = "";
        var formCategory = reportCategory;
        var ruleType = reportCategory == LogRuleCategory.User ? "event" : "file";
        var includeSubfolders = false;

        if (reportCategory == LogRuleCategory.File)
        {
            switch (columnName)
            {
                case "File":
                    value = entry.Subject;
                    break;
                case "Extension":
                    if (entry.Fields.TryGetValue("Extension", out var extension))
                        value = string.IsNullOrWhiteSpace(extension)
                            ? ""
                            : "*" + (extension.StartsWith('.') ? extension : "." + extension);
                    break;
                case "Folder":
                    value = entry.Folder;
                    ruleType = "folder";
                    includeSubfolders = true;
                    break;
                case "App":
                    value = entry.App;
                    formCategory = LogRuleCategory.App;
                    ruleType = "process";
                    break;
            }
        }
        else if (reportCategory == LogRuleCategory.App)
        {
            if (columnName == "App")
                value = entry.App;
            else if (columnName == "Path")
                value = entry.Path;
            ruleType = "process";
        }
        else if (reportCategory == LogRuleCategory.User && columnName == "Event")
        {
            value = entry.Subject;
        }

        value = value.Trim();
        if (value.Length == 0 || value == "*")
            return false;

        suggestion = new RuleSuggestion(value, formCategory, ruleType, includeSubfolders);
        return true;
    }

    public static bool TryBuildGroupCellRuleSuggestion(
        LogRuleCategory reportCategory,
        string columnName,
        string groupValue,
        out RuleSuggestion suggestion)
    {
        suggestion = default;
        var value = groupValue.Trim();
        if (value.Length == 0 || value.StartsWith("(unknown ", StringComparison.OrdinalIgnoreCase))
            return false;

        if (reportCategory == LogRuleCategory.File)
        {
            suggestion = columnName switch
            {
                "File" => new RuleSuggestion(value, LogRuleCategory.File, "file", false),
                "Extension" when !value.Equals("(no extension)", StringComparison.OrdinalIgnoreCase) =>
                    new RuleSuggestion(
                        "*" + (value.StartsWith('.') ? value : "." + value),
                        LogRuleCategory.File,
                        "file",
                        false),
                "Folder" => new RuleSuggestion(value, LogRuleCategory.File, "folder", true),
                "App" => new RuleSuggestion(value, LogRuleCategory.App, "process", false),
                _ => default
            };
        }
        else if (reportCategory == LogRuleCategory.App)
        {
            suggestion = columnName switch
            {
                "App" or "Path" => new RuleSuggestion(
                    value, LogRuleCategory.App, "process", false),
                _ => default
            };
        }

        return !string.IsNullOrWhiteSpace(suggestion.Value);
    }



    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.ColumnIndex < 0)
            return;

        _headerClickTimer.Stop();
        _pendingHeaderGrid = grid;
        _pendingHeaderColumnIndex = e.ColumnIndex;
        _headerClickTimer.Start();
    }

    private void Grid_ColumnHeaderMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _headerClickTimer.Stop();
        _pendingHeaderGrid = null;
        _pendingHeaderColumnIndex = -1;

        if (sender is not DataGridView grid ||
            e.ColumnIndex < 0 ||
            !TryGetHeaderGrouping(grid, grid.Columns[e.ColumnIndex], out var grouping))
        {
            return;
        }

        if (grid == gridFile)
        {
            _fileGroupBy = _fileGroupBy == grouping ? "None" : grouping;
            _fileGroupSortColumn = "Latest";
            _fileGroupSortAscending = false;
            _expandedFileGroups.Clear();
            _filePage = 0;
        }
        else
        {
            _appGroupBy = _appGroupBy == grouping ? "None" : grouping;
            _appGroupSortColumn = "Latest";
            _appGroupSortAscending = false;
            _expandedAppGroups.Clear();
            _appPage = 0;
        }

        UpdateGroupByControls();
        RefreshActiveTab();
    }

    private bool TryGetHeaderGrouping(
        DataGridView grid,
        DataGridViewColumn column,
        out string grouping)
    {
        var header = column.Tag as string ?? column.HeaderText;
        grouping = GetHeaderGrouping(
            grid == gridFile
                ? LogRuleCategory.File
                : grid == gridApp
                    ? LogRuleCategory.App
                    : LogRuleCategory.User,
            header) ?? "";
        return grouping.Length > 0;
    }

    public static string? GetHeaderGrouping(
        LogRuleCategory category,
        string header) =>
        category switch
        {
            LogRuleCategory.File => header switch
            {
                "Date" => "Date",
                "File" => "File name",
                "Extension" => "Extension",
                "Activity" => "Activity",
                "Folder" => "Folder",
                "App" => "Application",
                _ => null
            },
            LogRuleCategory.App => header switch
                {
                    "Date" => "Date",
                    "App" => "Application",
                    "Process ID" => "Process ID",
                    "Path" => "Path",
                    _ => null
                },
            _ => null
        };

    private void ApplyPendingHeaderSort()
    {
        _headerClickTimer.Stop();
        var grid = _pendingHeaderGrid;
        var columnIndex = _pendingHeaderColumnIndex;
        _pendingHeaderGrid = null;
        _pendingHeaderColumnIndex = -1;
        if (grid == null || grid.IsDisposed || columnIndex < 0 || columnIndex >= grid.Columns.Count)
            return;

        ApplyColumnSort(grid, columnIndex);
    }

    private void ApplyColumnSort(DataGridView grid, int columnIndex)
    {
        var column = grid.Columns[columnIndex];
        if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
            return;

        var originalHeaderText = column.Tag as string ?? column.HeaderText;
        var databaseColumn = grid == gridFile && _fileGroupBy != "None"
            ? GetFileGroupSortColumn(originalHeaderText)
            : grid == gridApp && _appGroupBy != "None"
                ? GetAppGroupSortColumn(originalHeaderText)
                : GetDatabaseSortColumn(grid, originalHeaderText);
        if (databaseColumn == null)
            return;

        if (grid == gridApp)
        {
            if (_appGroupBy != "None")
            {
                _appGroupSortAscending = string.Equals(_appGroupSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                    ? !_appGroupSortAscending
                    : true;
                _appGroupSortColumn = databaseColumn;
            }
            else
            {
                _appSortAscending = string.Equals(_appSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                    ? !_appSortAscending
                    : true;
                _appSortColumn = databaseColumn;
            }
            _appPage = 0;
        }
        else if (grid == gridUser)
        {
            _userSortAscending = string.Equals(_userSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                ? !_userSortAscending
                : true;
            _userSortColumn = databaseColumn;
            _userPage = 0;
        }
        else
        {
            if (_fileGroupBy != "None")
            {
                _fileGroupSortAscending = string.Equals(_fileGroupSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                    ? !_fileGroupSortAscending
                    : true;
                _fileGroupSortColumn = databaseColumn;
            }
            else
            {
                _fileSortAscending = string.Equals(_fileSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                    ? !_fileSortAscending
                    : true;
                _fileSortColumn = databaseColumn;
            }
            _filePage = 0;
        }

        UpdateSortGlyphs(
            grid,
            column,
            grid == gridFile && _fileGroupBy != "None"
                ? _fileGroupSortAscending
                : grid == gridApp && _appGroupBy != "None"
                    ? _appGroupSortAscending
                    : GetSortAscending(grid));
        RefreshActiveTab();
    }

    private string? GetFileGroupSortColumn(string headerText) =>
        headerText.Equals("Records", StringComparison.OrdinalIgnoreCase)
            ? "RecordCount"
            : headerText.Equals(FileGroupDisplayHeaderText(), StringComparison.OrdinalIgnoreCase)
                ? "GroupKey"
                : null;

    private string? GetAppGroupSortColumn(string headerText) =>
        headerText.Equals("Records", StringComparison.OrdinalIgnoreCase)
            ? "RecordCount"
            : headerText.Equals(AppGroupDisplayHeaderText(), StringComparison.OrdinalIgnoreCase)
                ? "GroupKey"
                : null;

    private string? GetDatabaseSortColumn(DataGridView grid, string headerText)
    {
        if (grid == gridApp)
        {
            return headerText switch
            {
                "ID" => "Id",
                "Date" => "EventDate",
                "Time" => "EventTime",
                "App" => "ProgramName",
                "Process ID" => "ProcessId",
                "Path" => "FilePath",
                _ => null
            };
        }

        if (grid == gridUser)
        {
            return headerText switch
            {
                "ID" => "Id",
                "Date" => "EventDate",
                "Time" => "EventTime",
                "Event" => "EventDescription",
                "User" => "UserName",
                "Computer" => "MachineName",
                _ => null
            };
        }

        return headerText switch
        {
            "ID" => "Id",
            "Date" => "COALESCE(NULLIF(DateClosed, ''), NULLIF(LastWriteDate, ''), NULLIF(FirstWriteDate, ''), NULLIF(DateOpened, ''), substr(CreatedAt, 1, 10))",
            "Time" => "COALESCE(NULLIF(TimeClosed, ''), NULLIF(LastWriteTime, ''), NULLIF(FirstWriteTime, ''), NULLIF(TimeOpened, ''), substr(CreatedAt, 12))",
            "File" => "FileName",
            "Extension" => "Extension",
            "Activity" => "COALESCE(NULLIF(InferredAction, ''), NULLIF(ActivityType, ''), '(unknown)')",
            "Folder" => "FolderPath",
            "App" => "ProcessName",
            _ => null
        };
    }

    private bool GetSortAscending(DataGridView grid) => grid == gridApp
        ? _appSortAscending
        : grid == gridUser ? _userSortAscending : _fileSortAscending;

    private static void UpdateSortGlyphs(DataGridView grid, DataGridViewColumn sortedColumn, bool ascending)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
            if (column.Tag is string originalHeaderText)
                column.HeaderText = originalHeaderText;
        }

        sortedColumn.HeaderCell.SortGlyphDirection = ascending ? SortOrder.Ascending : SortOrder.Descending;
        if (sortedColumn.Tag is string sortedHeaderText)
            sortedColumn.HeaderText = sortedHeaderText + (ascending ? " ▲" : " ▼");
    }

    private static string BuildOrderBy(string column, bool ascending)
    {
        var direction = ascending ? "ASC" : "DESC";
        return $"ORDER BY {column} {direction}, Id {direction}";
    }

    private void UpdateSelectionButtons()
    {
        var grid = GetActiveGrid();
        var selectedCount = grid?.SelectedRows.Count ?? 0;
        createRuleButton.Enabled = selectedCount == 1 &&
            (grid!.SelectedRows[0].Tag is LogViewEntry ||
             TryGetGroupRuleSuggestion(grid, grid.SelectedRows[0], out _));
        deleteButton.Enabled = selectedCount > 0 &&
            grid!.SelectedRows.Cast<DataGridViewRow>().Any(row =>
                row.Tag is LogViewEntry ||
                grid == gridFile && row.Tag is FileLogGroup ||
                grid == gridApp && row.Tag is AppLogGroup);
    }

    private bool TryGetGroupRuleSuggestion(
        DataGridView grid,
        DataGridViewRow row,
        out GroupRuleSuggestion suggestion)
    {
        suggestion = default;

        if (grid == gridFile && row.Tag is FileLogGroup fileGroup)
        {
            suggestion = _fileGroupBy switch
            {
                "File name" => new(fileGroup.Key, LogRuleCategory.File, "file", false),
                "Extension" when !fileGroup.Key.Equals("(no extension)", StringComparison.OrdinalIgnoreCase) =>
                    new("*" + (fileGroup.Key.StartsWith('.') ? fileGroup.Key : "." + fileGroup.Key),
                        LogRuleCategory.File, "file", false),
                "Folder" when !fileGroup.Key.Equals("(unknown folder)", StringComparison.OrdinalIgnoreCase) =>
                    new(fileGroup.Key, LogRuleCategory.File, "folder", true),
                "Application" when !fileGroup.Key.Equals("(unknown application)", StringComparison.OrdinalIgnoreCase) =>
                    new(fileGroup.Key, LogRuleCategory.App, "process", false),
                _ => default
            };
        }
        else if (grid == gridApp && row.Tag is AppLogGroup appGroup)
        {
            suggestion = _appGroupBy switch
            {
                "Application" when !appGroup.Key.Equals("(unknown application)", StringComparison.OrdinalIgnoreCase) =>
                    new(appGroup.Key, LogRuleCategory.App, "process", false),
                "Path" when !appGroup.Key.Equals("(unknown path)", StringComparison.OrdinalIgnoreCase) =>
                    new(appGroup.Key, LogRuleCategory.App, "process", false),
                _ => default
            };
        }

        return !string.IsNullOrWhiteSpace(suggestion.Value);
    }

    private DataGridView? GetActiveGrid()
    {
        if (tabs.SelectedTab == null) return null;
        if (tabs.SelectedTab.Text == "App Activity") return gridApp;
        if (tabs.SelectedTab.Text == "File Activity") return gridFile;
        if (tabs.SelectedTab.Text == "User Activity") return gridUser;
        return null;
    }

    private LogRuleCategory GetActiveCategory()
    {
        if (tabs.SelectedTab?.Text == "App Activity") return LogRuleCategory.App;
        if (tabs.SelectedTab?.Text == "User Activity") return LogRuleCategory.User;
        return LogRuleCategory.File;
    }

    private async void DeleteButton_Click(object? sender, EventArgs e)
    {
        await DeleteSelectedAsync(allowRuleCreation: true);
    }

    private async Task DeleteSelectedAsync(bool allowRuleCreation)
    {
        var grid = GetActiveGrid();
        if (grid == null)
            return;

        var selectedRows = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .ToList();
        var entries = selectedRows
            .Select(row => row.Tag as LogViewEntry)
            .Where(entry => entry != null && long.TryParse(entry.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Cast<LogViewEntry>()
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        var fileGroups = grid == gridFile
            ? selectedRows.Select(row => row.Tag).OfType<FileLogGroup>().ToList()
            : new List<FileLogGroup>();
        var appGroups = grid == gridApp
            ? selectedRows.Select(row => row.Tag).OfType<AppLogGroup>().ToList()
            : new List<AppLogGroup>();
        var groupedDeletion = fileGroups.Count > 0 || appGroups.Count > 0;
        var ids = entries
            .Select(entry => long.Parse(entry.Id, CultureInfo.InvariantCulture))
            .ToHashSet();

        foreach (var group in fileGroups)
            ids.UnionWith(ReadGroupRecordIds(
                "ActivityEvents", FileGroupExpression(), group.Key,
                dateStart.Value, dateEnd.Value));
        foreach (var group in appGroups)
            ids.UnionWith(ReadGroupRecordIds(
                "ProgramEvents", AppGroupExpression(), group.Key,
                dateStart.Value, dateEnd.Value));

        if (ids.Count == 0)
            return;

        var sectionName = tabs.SelectedTab?.Text ?? "activity";
        var category = GetActiveCategory();
        var selectedGroupCount = fileGroups.Count + appGroups.Count;
        using var deleteForm = new DeleteLogRecordsForm(
            ids.Count,
            sectionName,
            category,
            groupedDeletion,
            selectedGroupCount,
            allowRuleCreation);
        if (deleteForm.ShowDialog(this) != DialogResult.OK)
            return;

        if (groupedDeletion)
        {
            var finalApproval = MessageBox.Show(
                this,
                $"FINAL WARNING\r\n\r\n" +
                $"You are about to permanently delete {ids.Count:N0} record(s) from " +
                $"{selectedGroupCount:N0} selected group(s).\r\n\r\n" +
                "This cannot be undone and may severely affect the historical data in the logs.\r\n\r\n" +
                "Do you want to proceed?",
                "Confirm permanent grouped deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (finalApproval != DialogResult.Yes)
                return;
        }

        var createRules = deleteForm.CreateRules;

        var tableName = tabs.SelectedTab?.Text switch
        {
            "App Activity" => "ProgramEvents",
            "User Activity" => "UserEvents",
            _ => "ActivityEvents"
        };

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = $"Deleting {ids.Count:N0} selected record(s)...";

            var deleted = await ServicePipeClient.DeleteRecordsAsync(tableName, ids.ToArray());
            var rulesCreated = createRules ? CreateExclusionRulesForEntries(entries, deleteForm.MatchType) : 0;
            RemoveDeletedRowsFromGrid(grid, selectedRows, ids);
            var completionStatus = rulesCreated > 0
                ? $"Deleted {deleted:N0} selected record(s) and created {rulesCreated:N0} exclusion rule(s)."
                : $"Deleted {deleted:N0} selected record(s).";
            statusLabel.Text = completionStatus;
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                        return;
                    RefreshActiveTab();
                    statusLabel.Text = completionStatus;
                }));
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = "The selected records could not be deleted.";
            MessageBox.Show(
                this,
                "DeskPulse could not delete the selected log records.\n\n" + ex.Message,
                "Delete selected log records",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RemoveDeletedRowsFromGrid(
        DataGridView grid,
        IReadOnlyCollection<DataGridViewRow> selectedRows,
        IReadOnlySet<long> deletedIds)
    {
        var selected = selectedRows.ToHashSet();
        for (var rowIndex = grid.Rows.Count - 1; rowIndex >= 0; rowIndex--)
        {
            var row = grid.Rows[rowIndex];
            var remove = selected.Contains(row);
            if (!remove &&
                row.Tag is LogViewEntry entry &&
                long.TryParse(entry.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                remove = deletedIds.Contains(id);
            }

            if (remove)
                grid.Rows.RemoveAt(rowIndex);
        }

        grid.ClearSelection();
        UpdateSelectionButtons();
        UpdatePageStatus();
    }

    private List<long> ReadGroupRecordIds(
        string tableName,
        string groupExpression,
        string groupKey,
        DateTime start,
        DateTime endExclusive)
    {
        var allowedTable = tableName switch
        {
            "ActivityEvents" => "ActivityEvents",
            "ProgramEvents" => "ProgramEvents",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };

        var result = new List<long>();
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id
            FROM {allowedTable}
            WHERE CreatedAt >= $start AND CreatedAt < $end
              AND {groupExpression} = $groupKey;
            """;
        AddDateParameters(command, start, endExclusive);
        command.Parameters.AddWithValue("$groupKey", groupKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetInt64(0));
        return result;
    }


    private int CreateExclusionRulesForEntries(IReadOnlyList<LogViewEntry> entries, string matchType)
    {
        var category = GetActiveCategory();
        var settings = AppSettings.Load();
        var target = category switch
        {
            LogRuleCategory.App => settings.AppActivityRuleSettings,
            LogRuleCategory.File => settings.FileActivityRuleSettings,
            _ => settings.UserActivityRuleSettings
        };

        var created = 0;
        foreach (var entry in entries)
        {
            var rule = BuildDeletionExclusionRule(entry, category, matchType);
            if (rule == null)
                continue;

            var duplicate = target.Any(existing =>
                existing.RuleType.Equals(rule.RuleType, StringComparison.OrdinalIgnoreCase) &&
                existing.Value.Equals(rule.Value, StringComparison.OrdinalIgnoreCase) &&
                existing.Action.Equals(rule.Action, StringComparison.OrdinalIgnoreCase) &&
                existing.IncludeSubfolders == rule.IncludeSubfolders);

            if (duplicate)
                continue;

            // Specific rules must precede broad catch-all rules because the first match wins.
            target.Insert(0, rule);
            created++;
        }

        if (created > 0)
        {
            settings.Save();
            _settingsChanged?.Invoke();
        }

        return created;
    }

    public static ActivityRuleSetting? BuildDeletionExclusionRule(
        LogViewEntry entry,
        LogRuleCategory category,
        string matchType)
    {
        var path = (!string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.Subject).Trim();
        var ruleType = category switch
        {
            LogRuleCategory.App => "process",
            LogRuleCategory.File => "file",
            _ => "event"
        };
        var value = category switch
        {
            LogRuleCategory.App => path.Length > 0 ? path : entry.App.Trim(),
            LogRuleCategory.User => entry.Subject.Trim(),
            _ when matchType == DeleteLogRecordsForm.FileNameAnywhere => Path.GetFileName(path),
            _ when matchType == DeleteLogRecordsForm.FileExtension =>
                entry.Fields.TryGetValue("Extension", out var extension) && !string.IsNullOrWhiteSpace(extension)
                    ? "*" + (extension.StartsWith('.') ? extension : "." + extension)
                    : "*" + Path.GetExtension(path),
            _ when matchType == DeleteLogRecordsForm.Folder =>
                !string.IsNullOrWhiteSpace(entry.Folder) ? entry.Folder.Trim() : Path.GetDirectoryName(path) ?? "",
            _ when matchType == DeleteLogRecordsForm.Application => entry.App.Trim(),
            _ => path
        };

        if (category == LogRuleCategory.File)
        {
            if (matchType == DeleteLogRecordsForm.Folder)
                ruleType = "folder";
            else if (matchType == DeleteLogRecordsForm.Application)
                ruleType = "process";
        }

        value = value.Trim();
        if (value.Length == 0 || value == "*")
            return null;

        return new ActivityRuleSetting
        {
            Enabled = true,
            RuleType = ruleType,
            Action = "Exclude",
            Value = value,
            IncludeSubfolders = ruleType == "folder"
        };
    }

    private async void CreateRuleButton_Click(object? sender, EventArgs e)
    {
        var grid = GetActiveGrid();
        if (grid?.SelectedRows.Count != 1)
            return;

        var category = GetActiveCategory();
        var selectedTag = grid.SelectedRows[0].Tag;
        var formCategory = category;
        var ruleType = category switch
        {
            LogRuleCategory.App => "process",
            LogRuleCategory.File => "file",
            _ => "event"
        };
        var includeSubfolders = false;
        string suggestedValue;

        if (selectedTag is LogViewEntry entry)
        {
            suggestedValue = category switch
            {
                LogRuleCategory.App => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.App,
                LogRuleCategory.File => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.Subject,
                _ => entry.Subject
            };
        }
        else if (TryGetGroupRuleSuggestion(grid, grid.SelectedRows[0], out var groupSuggestion))
        {
            suggestedValue = groupSuggestion.Value;
            formCategory = groupSuggestion.FormCategory;
            ruleType = groupSuggestion.RuleType;
            includeSubfolders = groupSuggestion.IncludeSubfolders;
        }
        else
        {
            return;
        }

        await CreateRuleAsync(new RuleSuggestion(
            suggestedValue,
            formCategory,
            ruleType,
            includeSubfolders));
    }

    private async Task CreateRuleAsync(RuleSuggestion suggestion)
    {
        var category = GetActiveCategory();
        var isFileActivityProcessFilter =
            category == LogRuleCategory.File &&
            suggestion.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase);
        using var form = new AddLogRuleForm(
            suggestion.FormCategory,
            suggestion.Value,
            suggestion.RuleType,
            exclusionOnly: isFileActivityProcessFilter);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        var settings = AppSettings.Load();
        var ruleValue = isFileActivityProcessFilter
            ? AppSettings.NormalizeProcessName(form.RuleValue)
            : form.RuleValue;
        var rule = new ActivityRuleSetting
        {
            Enabled = form.RuleEnabled,
            RuleType = suggestion.RuleType,
            Action = form.IsInclude ? "Include" : "Exclude",
            Value = ruleValue,
            IncludeSubfolders = suggestion.IncludeSubfolders
        };

        var target = category switch
        {
            LogRuleCategory.App => settings.AppActivityRuleSettings,
            LogRuleCategory.File => settings.FileActivityRuleSettings,
            _ => settings.UserActivityRuleSettings
        };

        var duplicate = isFileActivityProcessFilter
            ? settings.IsFileActivityProcessFiltered(rule.Value)
            : target.Any(existing =>
                existing.RuleType.Equals(rule.RuleType, StringComparison.OrdinalIgnoreCase) &&
                existing.Value.Equals(rule.Value, StringComparison.OrdinalIgnoreCase) &&
                existing.Action.Equals(rule.Action, StringComparison.OrdinalIgnoreCase) &&
                existing.IncludeSubfolders == rule.IncludeSubfolders);

        if (duplicate)
        {
            var message = isFileActivityProcessFilter
                ? "This application is already in the filtered File Activity applications list."
                : "An equivalent rule already exists in this rules list.";
            MessageBox.Show(this, message, "Create rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var conflictingIds = new ConflictingRecordIds();
        if (form.CleanUpOldData && !form.IsInclude)
        {
            createRuleButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Checking all existing records against the new rule...";
            try
            {
                conflictingIds = await Task.Run(() => FindConflictingRecordIds(category, rule));
            }
            finally
            {
                Cursor = Cursors.Default;
                UpdateSelectionButtons();
            }
        }

        if (conflictingIds.TotalCount > 0)
        {
            var confirm = MessageBox.Show(
                this,
                $"This rule will remove {conflictingIds.TotalCount:N0} existing record(s) from the DeskPulse database.\n\n" +
                "The database will then be compacted. This action cannot be undone.\n\nAccept this change?",
                "Confirm rule and data cleanup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;
        }

        if (isFileActivityProcessFilter)
        {
            settings.FilteredFileActivityProcesses.Add(rule.Value);
        }
        else
        {
            // Specific rules must precede broad catch-all rules because the first match wins.
            target.Insert(0, rule);
        }

        if (conflictingIds.TotalCount > 0)
        {
            using var progressForm = new RuleCleanupProgressForm(
                "Add rule and clean database",
                "Applying rule and cleaning old data",
                progress =>
                {
                    progress.Report(new ExportProgressInfo(2, "2%   Saving the new rule"));
                    settings.Save();

                    progress.Report(new ExportProgressInfo(8, "8%   Sending cleanup request to DeskPulse service"));
                    var result = ServicePipeClient.RunDatabaseHousekeepingAsync(
                        reloadSettingsFirst: true).GetAwaiter().GetResult();
                    progress.Report(new ExportProgressInfo(98, "98%  Finalising cleaned database"));
                    return result;
                });

            if (progressForm.ShowDialog(this) != DialogResult.OK)
                return;
        }
        else
        {
            settings.Save();
            try
            {
                await ServicePipeClient.ReloadSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The rule was saved, but DeskPulse could not activate it in the running service.\n\n" +
                    ex.Message,
                    "Add rule to rules list",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        _settingsChanged?.Invoke();
        RefreshLog();
        var successMessage = isFileActivityProcessFilter
            ? "The application was added to the filtered File Activity applications list."
            : "The rule was added successfully.";
        MessageBox.Show(this, successMessage, "Create rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private ConflictingRecordIds FindConflictingRecordIds(LogRuleCategory category, ActivityRuleSetting rule)
    {
        var result = new ConflictingRecordIds();
        using var connection = OpenReadConnection();
        using (var busy = connection.CreateCommand()) { busy.CommandText = "PRAGMA busy_timeout=5000;"; busy.ExecuteNonQuery(); }

        if (category == LogRuleCategory.File)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, COALESCE(FullPath, ''), COALESCE(ProcessName, '') FROM ActivityEvents;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var fullPath = reader.GetString(1);
                var processName = reader.GetString(2);
                var matches = rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase)
                    ? AppPatternMatches(fullPath, processName, rule.Value)
                    : rule.RuleType.Equals("folder", StringComparison.OrdinalIgnoreCase)
                        ? FolderPatternMatches(fullPath, rule.Value, rule.IncludeSubfolders)
                        : FilePatternMatches(fullPath, rule.Value);
                if (matches)
                    result.ActivityIds.Add(id);
            }
        }
        else if (category == LogRuleCategory.App)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, COALESCE(FilePath, ''), COALESCE(ProgramName, '') FROM ProgramEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    if (AppPatternMatches(reader.GetString(1), reader.GetString(2), rule.Value)) result.ProgramIds.Add(reader.GetInt64(0));
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, COALESCE(FullPath, ''), COALESCE(ProcessName, '') FROM ActivityEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    if (AppPatternMatches(reader.GetString(1), reader.GetString(2), rule.Value)) result.ActivityIds.Add(reader.GetInt64(0));
            }
        }
        else
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, COALESCE(EventDescription, '') FROM UserEvents;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var description = reader.GetString(1);
                if (TextPatternMatches(description, rule.Value)) result.UserIds.Add(reader.GetInt64(0));
            }
        }

        return result;
    }

    private static bool FilePatternMatches(string fullPath, string pattern)
    {
        pattern = (pattern ?? "").Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        if (Path.IsPathRooted(pattern) && !ContainsWildcard(pattern))
        {
            try { return Path.GetFullPath(fullPath).Equals(Path.GetFullPath(pattern), StringComparison.OrdinalIgnoreCase); }
            catch { return fullPath.Equals(pattern, StringComparison.OrdinalIgnoreCase); }
        }

        var target = pattern.Contains(Path.DirectorySeparatorChar) || pattern.Contains(Path.AltDirectorySeparatorChar)
            ? fullPath
            : Path.GetFileName(fullPath);

        var normalizedValue = (target ?? "").Replace('/', '\\');
        var normalizedPattern = NormalizeWindowsGlobPattern(pattern.Replace('/', '\\'));
        var regex = BuildWindowsGlobRegex(normalizedPattern);

        return Regex.IsMatch(normalizedValue, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool FolderPatternMatches(string fullPath, string folder, bool includeSubfolders)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(fullPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFolder = Path.GetFullPath(folder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var containingFolder = Path.GetDirectoryName(normalizedPath) ?? "";

            return containingFolder.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
                   (includeSubfolders &&
                    containingFolder.StartsWith(
                        normalizedFolder + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeWindowsGlobPattern(string pattern)
    {
        return string.Join("\\", pattern.Split('\\').Select(segment => segment == "*.*" ? "*" : segment));
    }

    private static string BuildWindowsGlobRegex(string pattern)
    {
        var builder = new System.Text.StringBuilder("^");

        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];

            if (current == '\\' && index + 3 < pattern.Length &&
                pattern[index + 1] == '*' && pattern[index + 2] == '*' && pattern[index + 3] == '\\')
            {
                builder.Append(@"\\(?:[^\\]+\\)*");
                index += 3;
                continue;
            }

            if (current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                builder.Append(".*");
                index++;
                continue;
            }

            if (current == '*')
            {
                builder.Append(@"[^\\]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append(@"[^\\]");
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }

    private static bool AppPatternMatches(string filePath, string processName, string pattern)
    {
        pattern = Environment.ExpandEnvironmentVariables((pattern ?? "").Trim().Trim('"'));
        var containsPathSeparator =
            pattern.Contains(Path.DirectorySeparatorChar) ||
            pattern.Contains(Path.AltDirectorySeparatorChar);
        if (containsPathSeparator && ContainsWildcard(pattern))
            return !string.IsNullOrWhiteSpace(filePath) &&
                FilePatternMatches(filePath, pattern);

        if (Path.IsPathRooted(pattern))
        {
            try { if (Path.GetFullPath(filePath).Equals(Path.GetFullPath(pattern), StringComparison.OrdinalIgnoreCase)) return true; }
            catch { }
        }
        var ruleName = Path.GetFileNameWithoutExtension(pattern);
        var process = Path.GetFileNameWithoutExtension(processName);
        return TextPatternMatches(process, ruleName) || TextPatternMatches(Path.GetFileName(filePath), Path.GetFileName(pattern));
    }

    private static bool TextPatternMatches(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern)) return false;
        if (!ContainsWildcard(pattern)) return value.Equals(pattern, StringComparison.OrdinalIgnoreCase) || value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsWildcard(string value) => value.Contains('*') || value.Contains('?');

    private sealed class ConflictingRecordIds
    {
        public List<long> ActivityIds { get; } = new();
        public List<long> ProgramIds { get; } = new();
        public List<long> UserIds { get; } = new();
        public int TotalCount => ActivityIds.Count + ProgramIds.Count + UserIds.Count;
    }


    private void ExportButton_Click(object? sender, EventArgs e)
    {
        var grid = GetActiveGrid();
        var total = GetActiveTotal();
        if (grid == null || total == 0)
        {
            MessageBox.Show(this, "There are no records in the selected date range to export.", "Export log", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sectionName = tabs.SelectedTab?.Text ?? "Log";
        var safeSectionName = string.Concat(sectionName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Replace(" ", "-");

        using var dialog = new SaveFileDialog
        {
            Title = "Export complete log view",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = $"DeskPulse-{safeSectionName}-{dateStart.Value:yyyyMMdd}-{dateEnd.Value:yyyyMMdd}.xlsx"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var closeAfterSuccessfulExport = false;
        try
        {
            Cursor = Cursors.WaitCursor;
            var start = dateStart.Value;
            var endExclusive = dateEnd.Value;
            if (endExclusive <= start)
            {
                MessageBox.Show(this, "The end date and time must be later than the start.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var exportedColumns = grid.Columns
                .Cast<DataGridViewColumn>()
                .Where(column => column.Visible && column is not DataGridViewButtonColumn)
                .OrderBy(column => column.DisplayIndex)
                .Select(column => column.HeaderText)
                .ToList();

            using var progressForm = new ExcelExportProgressForm(
                sectionName,
                progress => ExportCompleteLog(
                    dialog.FileName,
                    sectionName,
                    exportedColumns,
                    start,
                    endExclusive,
                    progress));

            SetLogExportInProgress(true);
            var result = progressForm.ShowDialog(this);
            if (result != DialogResult.OK)
            {
                var exportError = progressForm.ExportError;
                if (exportError != null)
                    throw exportError;

                return;
            }

            statusLabel.Text =
                $"Exported all {progressForm.ExportedCount:N0} record(s) from {sectionName}.";
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
            closeAfterSuccessfulExport = true;
        }
        catch (Exception ex)
        {
            statusLabel.Text = "The current view could not be exported.";
            MessageBox.Show(this, "DeskPulse could not export the current log view.\n\n" + ex.Message, "Export current view", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetLogExportInProgress(false);
            Cursor = Cursors.Default;
        }

        if (closeAfterSuccessfulExport)
            Close();
    }

    private int ExportCompleteLog(
        string fileName,
        string sectionName,
        IReadOnlyList<string> exportedColumns,
        DateTime start,
        DateTime endExclusive,
        IProgress<ExportProgressInfo> progress)
    {
        progress.Report(new ExportProgressInfo(2, $"2%   Reading all records from {sectionName}"));
        var entries = sectionName switch
        {
            "App Activity" => ReadAppEntries(start, endExclusive, 0, usePaging: false),
            "User Activity" => ReadUserEntries(start, endExclusive, 0, usePaging: false),
            _ => ReadFileEntries(start, endExclusive, 0, usePaging: false)
        };

        progress.Report(new ExportProgressInfo(8, $"8%   Preparing {entries.Count:N0} record(s)"));
        using var workbook = new XLWorkbook();
        var worksheetName = sectionName.Length > 31 ? sectionName[..31] : sectionName;
        var worksheet = workbook.Worksheets.Add(worksheetName);

        for (var columnIndex = 0; columnIndex < exportedColumns.Count; columnIndex++)
        {
            var cell = worksheet.Cell(1, columnIndex + 1);
            cell.Value = exportedColumns[columnIndex];
            cell.Style.Font.Bold = true;
        }

        var progressInterval = Math.Max(1, entries.Count / 200);
        for (var rowIndex = 0; rowIndex < entries.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < exportedColumns.Count; columnIndex++)
            {
                var value = GetExportCellValue(entries[rowIndex], exportedColumns[columnIndex]);
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value?.ToString() ?? string.Empty;
            }

            if (rowIndex % progressInterval == 0 || rowIndex == entries.Count - 1)
            {
                var percent = 8 + (int)Math.Round(((rowIndex + 1) / (double)Math.Max(1, entries.Count)) * 82);
                progress.Report(new ExportProgressInfo(percent, $"{percent}%  Writing record {rowIndex + 1:N0} of {entries.Count:N0}"));
            }
        }

        progress.Report(new ExportProgressInfo(92, "92%  Formatting workbook"));
        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.Columns().AdjustToContents(8, 60);
        progress.Report(new ExportProgressInfo(96, "96%  Saving workbook"));
        workbook.SaveAs(fileName);
        return entries.Count;
    }

    private void SetLogExportInProgress(bool inProgress)
    {
        exportButton.Enabled = !inProgress;
        refreshButton.Enabled = !inProgress;
        tabs.Enabled = !inProgress;
        dateStart.Enabled = !inProgress;
        dateEnd.Enabled = !inProgress;
    }

    private static object GetExportCellValue(LogViewEntry entry, string columnName)
    {
        return columnName switch
        {
            "ID" => entry.Id,
            "Date" => entry.Date,
            "Time" => entry.Time,
            "File" => entry.Subject,
            "Extension" => entry.Fields.TryGetValue("Extension", out var extension) ? extension : "",
            "Activity" => GetFileActivity(entry),
            "Folder" => entry.Folder,
            "App" => entry.App,
            "Process ID" => entry.ProcessId,
            "Path" => entry.Path,
            "Event" => entry.Subject,
            "User" => entry.App,
            "Computer" => entry.Fields.TryGetValue("Computer", out var computer) ? computer : "",
            _ => entry.Fields.TryGetValue(columnName, out var value) ? value : ""
        };
    }

    private void ApplyPageSizeButton_Click(object? sender, EventArgs e)
    {
        _pageSize = Math.Clamp((int)pageSizeInput.Value, 1, MaximumPageSize);
        SavePageSize(_pageSize);
        ResetPages();
        RefreshLog();
    }

    private void GroupByCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updatingGroupByCombo)
            return;

        var selected = groupByCombo.SelectedItem?.ToString() ?? "None";
        if (tabs.SelectedTab?.Text == "App Activity")
        {
            _appGroupBy = selected;
            _appGroupSortColumn = "Latest";
            _appGroupSortAscending = false;
            _expandedAppGroups.Clear();
            _appPage = 0;
        }
        else
        {
            _fileGroupBy = selected;
            _fileGroupSortColumn = "Latest";
            _fileGroupSortAscending = false;
            _expandedFileGroups.Clear();
            _filePage = 0;
        }

        if (IsHandleCreated) RefreshActiveTab();
    }

    private void UpdateGroupByControls()
    {
        var tabName = tabs.SelectedTab?.Text;
        var supportsGrouping = tabName is "File Activity" or "App Activity";
        groupByLabel.Visible = supportsGrouping;
        groupByCombo.Visible = false;
        if (!supportsGrouping)
            return;

        var options = tabName == "App Activity"
            ? new[] { "None", "Date", "Application", "Process ID", "Path" }
            : new[] { "None", "Date", "File name", "Extension", "Folder", "Application", "Activity" };
        var selected = tabName == "App Activity" ? _appGroupBy : _fileGroupBy;
        groupByLabel.Text = selected == "None"
            ? "Double-click a heading to group"
            : $"Grouped: {selected}";

        _updatingGroupByCombo = true;
        try
        {
            groupByCombo.Items.Clear();
            groupByCombo.Items.AddRange(options);
            groupByCombo.SelectedItem = options.Contains(selected, StringComparer.Ordinal) ? selected : "None";
        }
        finally
        {
            _updatingGroupByCombo = false;
        }
    }

    private void TimeFormatCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _use12HourTime = string.Equals(timeFormatCombo.SelectedItem?.ToString(), "12-hour", StringComparison.Ordinal);
        SaveUse12HourTime(_use12HourTime);
        if (IsHandleCreated) RefreshActiveTab();
    }

    private static bool LoadUse12HourTime()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ViewSettingsRegistryPath);
            return key?.GetValue("ViewLogUse12HourTime") is int value && value != 0;
        }
        catch { return false; }
    }

    private static void SaveUse12HourTime(bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ViewSettingsRegistryPath);
            key?.SetValue("ViewLogUse12HourTime", value ? 1 : 0, RegistryValueKind.DWord);
        }
        catch { }
    }

    private static int LoadPageSize()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ViewSettingsRegistryPath);
            return key?.GetValue("ViewLogPageSize") is int value ? Math.Clamp(value, 1, MaximumPageSize) : DefaultPageSize;
        }
        catch { return DefaultPageSize; }
    }

    private static void SavePageSize(int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ViewSettingsRegistryPath);
            key?.SetValue("ViewLogPageSize", value, RegistryValueKind.DWord);
        }
        catch { }
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        ResetPages();
        RefreshLog();
    }

    private void FirstPageButton_Click(object? sender, EventArgs e)
    {
        if (GetActivePage() == 0) return;
        SetActivePage(0);
        RefreshActiveTab();
    }

    private void PreviousPageButton_Click(object? sender, EventArgs e)
    {
        var page = GetActivePage();
        if (page <= 0) return;
        SetActivePage(page - 1);
        RefreshActiveTab();
    }

    private void NextPageButton_Click(object? sender, EventArgs e)
    {
        var page = GetActivePage();
        var total = GetActiveTotal();
        if ((page + 1) * _pageSize >= total) return;
        SetActivePage(page + 1);
        RefreshActiveTab();
    }

    private void LastPageButton_Click(object? sender, EventArgs e)
    {
        var total = GetActiveTotal();
        var lastPage = Math.Max(0, (int)Math.Ceiling(total / (double)_pageSize) - 1);
        if (GetActivePage() == lastPage) return;
        SetActivePage(lastPage);
        RefreshActiveTab();
    }

    private void ResetPages()
    {
        _appPage = 0;
        _filePage = 0;
        _userPage = 0;
    }

    private int GetActivePage() => tabs.SelectedTab?.Text switch
    {
        "App Activity" => _appPage,
        "User Activity" => _userPage,
        _ => _filePage
    };

    private int GetActiveTotal() => tabs.SelectedTab?.Text switch
    {
        "App Activity" => _appTotal,
        "User Activity" => _userTotal,
        _ => _fileTotal
    };

    private void SetActivePage(int page)
    {
        page = Math.Max(0, page);
        switch (tabs.SelectedTab?.Text)
        {
            case "App Activity": _appPage = page; break;
            case "User Activity": _userPage = page; break;
            default: _filePage = page; break;
        }
    }

    private int ClampPage(int page, int total)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
        return Math.Clamp(page, 0, totalPages - 1);
    }

    private void ClampAllPages()
    {
        _appPage = ClampPage(_appPage, _appTotal);
        _filePage = ClampPage(_filePage, _fileTotal);
        _userPage = ClampPage(_userPage, _userTotal);
    }

    private void UpdatePagingControls()
    {
        var total = GetActiveTotal();
        var page = GetActivePage();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
        if (page >= totalPages)
        {
            page = totalPages - 1;
            SetActivePage(page);
        }

        firstPageButton.Enabled = page > 0;
        previousPageButton.Enabled = page > 0;
        nextPageButton.Enabled = (page + 1) * _pageSize < total;
        lastPageButton.Enabled = (page + 1) * _pageSize < total;
        exportButton.Enabled = total > 0 && GetActiveGrid()?.Rows.Count > 0;
        pageLabel.Text = IsActiveTabGrouped()
            ? $"Page {page + 1:N0} of {totalPages:N0} ({total:N0} groups)"
            : $"Page {page + 1:N0} of {totalPages:N0} ({total:N0} records)";
    }

    private void UpdatePageStatus()
    {
        var total = GetActiveTotal();
        var page = GetActivePage();
        var visibleRows = GetActiveGrid()?.Rows.Count ?? 0;

        if (total <= 0 || visibleRows <= 0)
        {
            statusLabel.Text = "Showing 0 of 0 records.";
            return;
        }

        var firstRecord = (page * _pageSize) + 1;
        var lastRecord = Math.Min(firstRecord + visibleRows - 1, total);
        statusLabel.Text = IsActiveTabGrouped()
            ? $"Showing groups {firstRecord:N0} to {lastRecord:N0} of {total:N0}. Double-click a group to expand or collapse it."
            : $"Showing {firstRecord:N0} to {lastRecord:N0} of {total:N0} records.";
    }

    private bool IsActiveTabGrouped() => tabs.SelectedTab?.Text switch
    {
        "File Activity" => _fileGroupBy != "None",
        "App Activity" => _appGroupBy != "None",
        _ => false
    };

    private void RefreshActiveTab()
    {
        var start = dateStart.Value;
        var endExclusive = dateEnd.Value;
        if (endExclusive <= start) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Reading log page...";
            Application.DoEvents();
            _updatingCalendarCells = true;
            try
            {
                switch (tabs.SelectedTab?.Text)
                {
                    case "App Activity":
                        _appTotal = _appGroupBy == "None"
                            ? CountEntries("ProgramEvents", start, endExclusive)
                            : CountAppGroups(start, endExclusive);
                        _appPage = ClampPage(_appPage, _appTotal);
                        if (_appGroupBy == "None")
                            PopulateAppGrid(ReadAppEntries(start, endExclusive, _appPage));
                        else
                            PopulateAppGroups(ReadAppGroups(start, endExclusive, _appPage), start, endExclusive);
                        gridApp.ClearSelection();
                        break;
                    case "User Activity":
                        _userTotal = CountEntries("UserEvents", start, endExclusive);
                        _userPage = ClampPage(_userPage, _userTotal);
                        PopulateUserGrid(ReadUserEntries(start, endExclusive, _userPage));
                        gridUser.ClearSelection();
                        break;
                    default:
                        if (_fileGroupBy == "None")
                        {
                            _fileTotal = CountEntries("ActivityEvents", start, endExclusive);
                            _filePage = ClampPage(_filePage, _fileTotal);
                            PopulateFileGrid(ReadFileEntries(start, endExclusive, _filePage));
                        }
                        else
                        {
                            _fileTotal = CountFileGroups(start, endExclusive);
                            _filePage = ClampPage(_filePage, _fileTotal);
                            PopulateFileGroups(ReadFileGroups(start, endExclusive, _filePage), start, endExclusive);
                        }
                        gridFile.ClearSelection();
                        break;
                }
            }
            finally
            {
                _updatingCalendarCells = false;
            }

            UpdateSelectionButtons();
            UpdatePagingControls();
            UpdatePageStatus();
        }
        catch (Exception ex)
        {
            statusLabel.Text = "The log page could not be read.";
            MessageBox.Show(this, "DeskPulse could not read the activity log.\n\n" + ex.Message, "View Log", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RefreshLog()
    {
        var start = dateStart.Value;
        var endExclusive = dateEnd.Value;

        if (endExclusive <= start)
        {
            MessageBox.Show(this, "The end date and time must be later than the start.", "View Log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Reading log...";
            Application.DoEvents();

            _fileTotal = _fileGroupBy == "None" ? CountEntries("ActivityEvents", start, endExclusive) : CountFileGroups(start, endExclusive);
            _appTotal = _appGroupBy == "None"
                ? CountEntries("ProgramEvents", start, endExclusive)
                : CountAppGroups(start, endExclusive);
            _userTotal = CountEntries("UserEvents", start, endExclusive);
            ClampAllPages();

            var fileEntries = _fileGroupBy == "None" ? ReadFileEntries(start, endExclusive, _filePage) : new List<LogViewEntry>();
            var fileGroups = _fileGroupBy == "None" ? new List<FileLogGroup>() : ReadFileGroups(start, endExclusive, _filePage);
            var appEntries = _appGroupBy == "None" ? ReadAppEntries(start, endExclusive, _appPage) : new List<LogViewEntry>();
            var appGroups = _appGroupBy == "None" ? new List<AppLogGroup>() : ReadAppGroups(start, endExclusive, _appPage);
            var userEntries = ReadUserEntries(start, endExclusive, _userPage);

            _updatingCalendarCells = true;
            try
            {
                if (_fileGroupBy == "None") PopulateFileGrid(fileEntries); else PopulateFileGroups(fileGroups, start, endExclusive);
                if (_appGroupBy == "None") PopulateAppGrid(appEntries); else PopulateAppGroups(appGroups, start, endExclusive);
                PopulateUserGrid(userEntries);
            }
            finally
            {
                _updatingCalendarCells = false;
            }

            gridApp.ClearSelection();
            gridFile.ClearSelection();
            gridUser.ClearSelection();
            UpdateSelectionButtons();

            UpdatePagingControls();
            UpdatePageStatus();
        }
        catch (Exception ex)
        {
            statusLabel.Text = "The log could not be read.";
            MessageBox.Show(this, "DeskPulse could not read the activity log.\n\n" + ex.Message, "View Log", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private List<LogViewEntry> ReadFileEntries(DateTime start, DateTime endExclusive, int page, bool usePaging = true)
    {
        var pagingClause = usePaging ? "LIMIT $limit OFFSET $offset;" : "";
        var sql = $"""
            SELECT Id, CreatedAt, ActivityType, FullPath, FolderPath, FileName, Extension,
                   DateOpened, TimeOpened, SizeAtOpening, FirstWriteDate, FirstWriteTime,
                   LastWriteDate, LastWriteTime, WriteCount, SizeAtLastWrite, DateClosed,
                   TimeClosed, SizeAtClosing, InferredAction, ProcessName, ProcessId, Note,
                   Scope, WindowsSid, SessionId, ShowInCalendarView
            FROM ActivityEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            {BuildOrderBy(_fileSortColumn, _fileSortAscending)}
            {pagingClause}
            """;

        return ReadEntries(sql, start, endExclusive, page, reader =>
        {
            var createdAt = ReadText(reader, 1);
            var fullPath = ReadText(reader, 3);
            var folder = ReadText(reader, 4);
            var file = ReadText(reader, 5);
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(fullPath) ?? "";
            if (string.IsNullOrWhiteSpace(file)) file = Path.GetFileName(fullPath);

            var fields = new Dictionary<string, string>
            {
                ["ID"] = ReadText(reader, 0), ["Created At"] = createdAt, ["Activity Type"] = ReadText(reader, 2),
                ["Full Path"] = fullPath, ["Folder"] = folder, ["File"] = file, ["Extension"] = ReadText(reader, 6),
                ["Date Opened"] = ReadText(reader, 7), ["Time Opened"] = ReadText(reader, 8), ["Size At Opening"] = ReadText(reader, 9),
                ["First Write Date"] = ReadText(reader, 10), ["First Write Time"] = ReadText(reader, 11),
                ["Last Write Date"] = ReadText(reader, 12), ["Last Write Time"] = ReadText(reader, 13),
                ["Write Count"] = ReadText(reader, 14), ["Size At Last Write"] = ReadText(reader, 15),
                ["Date Closed"] = ReadText(reader, 16), ["Time Closed"] = ReadText(reader, 17), ["Size At Closing"] = ReadText(reader, 18),
                ["Inferred Action"] = ReadText(reader, 19), ["Process"] = ReadText(reader, 20), ["Process ID"] = ReadText(reader, 21), ["Note"] = ReadText(reader, 22),
                ["Scope"] = ReadText(reader, 23), ["Windows SID"] = ReadText(reader, 24), ["Session ID"] = ReadText(reader, 25),
                ["Show in Calendar View"] = ReadCalendarFlag(reader, 26) ? "Yes" : "No"
            };

            return new LogViewEntry(ReadText(reader, 0), createdAt, EventDate(createdAt, ReadText(reader, 7), ReadText(reader, 10), ReadText(reader, 12), ReadText(reader, 16)),
                EventTime(createdAt, ReadText(reader, 8), ReadText(reader, 11), ReadText(reader, 13), ReadText(reader, 17)),
                file, folder, ReadText(reader, 20), ReadText(reader, 21), fullPath, ReadCalendarFlag(reader, 26), fields);
        }, usePaging);
    }

    private List<LogViewEntry> ReadAppEntries(DateTime start, DateTime endExclusive, int page, bool usePaging = true)
    {
        var pagingClause = usePaging ? "LIMIT $limit OFFSET $offset;" : "";
        var sql = $"""
            SELECT Id, CreatedAt, EventDate, EventTime, EventDescription, ProgramName,
                   ProcessId, FilePath, WindowTitle, UserName, MachineName, AppVersion, Note,
                   Scope, WindowsSid, SessionId, ShowInCalendarView
            FROM ProgramEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            {BuildOrderBy(_appSortColumn, _appSortAscending)}
            {pagingClause}
            """;

        return ReadEntries(sql, start, endExclusive, page, CreateAppEntry, usePaging);
    }

    private LogViewEntry CreateAppEntry(SqliteDataReader reader)
    {
        var fields = new Dictionary<string, string>
        {
            ["ID"] = ReadText(reader, 0), ["Created At"] = ReadText(reader, 1), ["Date"] = ReadText(reader, 2), ["Time"] = ReadText(reader, 3),
            ["Event"] = ReadText(reader, 4), ["App"] = ReadText(reader, 5), ["Process ID"] = ReadText(reader, 6),
            ["App Path"] = ReadText(reader, 7), ["Window Title"] = ReadText(reader, 8), ["User"] = ReadText(reader, 9),
            ["Computer"] = ReadText(reader, 10), ["DeskPulse Version"] = ReadText(reader, 11), ["Note"] = ReadText(reader, 12),
            ["Scope"] = ReadText(reader, 13), ["Windows SID"] = ReadText(reader, 14), ["Session ID"] = ReadText(reader, 15),
            ["Show in Calendar View"] = ReadCalendarFlag(reader, 16) ? "Yes" : "No"
        };
        return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2),
            FormatDisplayTime(ReadText(reader, 3)), ReadText(reader, 5), "", ReadText(reader, 5),
            ReadText(reader, 6), ReadText(reader, 7), ReadCalendarFlag(reader, 16), fields);
    }

    private List<LogViewEntry> ReadUserEntries(DateTime start, DateTime endExclusive, int page, bool usePaging = true)
    {
        var pagingClause = usePaging ? "LIMIT $limit OFFSET $offset;" : "";
        var sql = $"""
            SELECT Id, CreatedAt, EventDate, EventTime, EventDescription, UserName,
                   MachineName, ProcessName, ProcessId, AppVersion, Note,
                   Scope, WindowsSid, SessionId, ShowInCalendarView
            FROM UserEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            {BuildOrderBy(_userSortColumn, _userSortAscending)}
            {pagingClause}
            """;

        return ReadEntries(sql, start, endExclusive, page, reader =>
        {
            var fields = new Dictionary<string, string>
            {
                ["ID"] = ReadText(reader, 0), ["Created At"] = ReadText(reader, 1), ["Date"] = ReadText(reader, 2), ["Time"] = ReadText(reader, 3),
                ["Event"] = ReadText(reader, 4), ["User"] = ReadText(reader, 5), ["Computer"] = ReadText(reader, 6),
                ["Process"] = ReadText(reader, 7), ["Process ID"] = ReadText(reader, 8), ["DeskPulse Version"] = ReadText(reader, 9), ["Note"] = ReadText(reader, 10),
                ["Scope"] = ReadText(reader, 11), ["Windows SID"] = ReadText(reader, 12), ["Session ID"] = ReadText(reader, 13),
                ["Show in Calendar View"] = ReadCalendarFlag(reader, 14) ? "Yes" : "No"
            };
            return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), FormatDisplayTime(ReadText(reader, 3)), ReadText(reader, 4), "", ReadText(reader, 5), ReadText(reader, 8), "", ReadCalendarFlag(reader, 14), fields);
        }, usePaging);
    }

    private List<LogViewEntry> ReadEntries(string sql, DateTime start, DateTime endExclusive, int page, Func<SqliteDataReader, LogViewEntry> factory, bool usePaging = true, string? groupKey = null)
    {
        var result = new List<LogViewEntry>();
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endExclusive.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        if (usePaging)
        {
            command.Parameters.AddWithValue("$limit", _pageSize);
            command.Parameters.AddWithValue("$offset", Math.Max(0, page) * _pageSize);
        }
        if (groupKey != null) command.Parameters.AddWithValue("$groupKey", groupKey);
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(factory(reader));
        return result;
    }

    private int CountEntries(string tableName, DateTime start, DateTime endExclusive)
    {
        var allowedTable = tableName switch
        {
            "ActivityEvents" => "ActivityEvents",
            "ProgramEvents" => "ProgramEvents",
            "UserEvents" => "UserEvents",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };

        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {allowedTable} WHERE CreatedAt >= $start AND CreatedAt < $end;";
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endExclusive.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string ReadText(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
    private static bool ReadCalendarFlag(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;

    private static string EventDate(string createdAt, params string[] candidates)
    {
        foreach (var candidate in candidates) if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
        return createdAt.Length >= 10 ? createdAt[..10] : createdAt;
    }

    private string EventTime(string createdAt, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate)) return FormatDisplayTime(candidate);
        }

        return createdAt.Length >= 19 ? FormatDisplayTime(createdAt.Substring(11)) : "";
    }

    private string FormatDisplayTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOfAny(new[] { '.', ',' });
        if (separatorIndex >= 0) trimmed = trimmed[..separatorIndex];

        if (!TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var time))
            return trimmed;

        var dateTime = DateTime.Today.Add(time);
        return dateTime.ToString(_use12HourTime ? "h:mm:ss tt" : "HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private string FileGroupExpression() => _fileGroupBy switch
    {
        "Date" => "COALESCE(NULLIF(DateClosed, ''), NULLIF(LastWriteDate, ''), NULLIF(FirstWriteDate, ''), NULLIF(DateOpened, ''), substr(CreatedAt, 1, 10))",
        "File name" => "COALESCE(NULLIF(FileName, ''), FullPath, '(unknown)')",
        "Extension" => "COALESCE(NULLIF(Extension, ''), '(no extension)')",
        "Folder" => "COALESCE(NULLIF(FolderPath, ''), '(unknown folder)')",
        "Application" => "COALESCE(NULLIF(ProcessName, ''), '(unknown application)')",
        "Activity" => "COALESCE(NULLIF(InferredAction, ''), NULLIF(ActivityType, ''), '(unknown activity)')",
        _ => "''"
    };

    private string AppGroupExpression() => _appGroupBy switch
    {
        "Date" => "COALESCE(NULLIF(EventDate, ''), substr(CreatedAt, 1, 10))",
        "Application" => "COALESCE(NULLIF(ProgramName, ''), '(unknown application)')",
        "Process ID" => "COALESCE(CAST(ProcessId AS TEXT), '(unknown process)')",
        "Path" => "COALESCE(NULLIF(FilePath, ''), '(unknown path)')",
        _ => "''"
    };

    private int CountAppGroups(DateTime start, DateTime endExclusive)
    {
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM (SELECT {AppGroupExpression()} AS GroupKey FROM ProgramEvents WHERE CreatedAt >= $start AND CreatedAt < $end GROUP BY GroupKey);";
        AddDateParameters(command, start, endExclusive);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private List<AppLogGroup> ReadAppGroups(DateTime start, DateTime endExclusive, int page)
    {
        var result = new List<AppLogGroup>();
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        var groupSortColumn = _appGroupSortColumn is "GroupKey" or "RecordCount" or "Latest"
            ? _appGroupSortColumn
            : "Latest";
        var groupSortDirection = _appGroupSortAscending ? "ASC" : "DESC";
        command.CommandText = $"""
            WITH AllGroups AS
            (
                SELECT {AppGroupExpression()} AS GroupKey,
                       COUNT(*) AS RecordCount,
                       MAX(CreatedAt) AS Latest,
                       SUM(CASE WHEN ShowInCalendarView <> 0 THEN 1 ELSE 0 END) AS CalendarCount
                FROM ProgramEvents
                WHERE CreatedAt >= $start AND CreatedAt < $end
                GROUP BY GroupKey
            )
            SELECT GroupKey, RecordCount, Latest, CalendarCount
            FROM AllGroups
            ORDER BY {groupSortColumn} {groupSortDirection}, GroupKey ASC
            LIMIT $limit OFFSET $offset;
            """;
        AddDateParameters(command, start, endExclusive);
        command.Parameters.AddWithValue("$limit", _pageSize);
        command.Parameters.AddWithValue("$offset", Math.Max(0, page) * _pageSize);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(new AppLogGroup(
                ReadText(reader, 0),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
        return result;
    }

    private List<LogViewEntry> ReadAppGroupEntries(DateTime start, DateTime endExclusive, string key)
    {
        var sql = $"""
            SELECT Id, CreatedAt, EventDate, EventTime, EventDescription, ProgramName,
                   ProcessId, FilePath, WindowTitle, UserName, MachineName, AppVersion, Note,
                   Scope, WindowsSid, SessionId, ShowInCalendarView
            FROM ProgramEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end AND {AppGroupExpression()} = $groupKey
            {BuildOrderBy(_appSortColumn, _appSortAscending)};
            """;
        return ReadEntries(sql, start, endExclusive, 0, CreateAppEntry, false, key);
    }

    private int CountFileGroups(DateTime start, DateTime endExclusive)
    {
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM (SELECT {FileGroupExpression()} AS GroupKey FROM ActivityEvents WHERE CreatedAt >= $start AND CreatedAt < $end GROUP BY GroupKey);";
        AddDateParameters(command, start, endExclusive);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private List<FileLogGroup> ReadFileGroups(DateTime start, DateTime endExclusive, int page)
    {
        var result = new List<FileLogGroup>();
        using var connection = OpenReadConnection();
        using var command = connection.CreateCommand();
        var groupSortColumn = _fileGroupSortColumn is "GroupKey" or "RecordCount" or "Latest"
            ? _fileGroupSortColumn
            : "Latest";
        var groupSortDirection = _fileGroupSortAscending ? "ASC" : "DESC";
        command.CommandText = $"""
            WITH AllGroups AS
            (
                SELECT {FileGroupExpression()} AS GroupKey,
                       COUNT(*) AS RecordCount,
                       MAX(CreatedAt) AS Latest,
                       SUM(CASE WHEN ShowInCalendarView <> 0 THEN 1 ELSE 0 END) AS CalendarCount
                FROM ActivityEvents
                WHERE CreatedAt >= $start AND CreatedAt < $end
                GROUP BY GroupKey
            )
            SELECT GroupKey, RecordCount, Latest, CalendarCount
            FROM AllGroups
            ORDER BY {groupSortColumn} {groupSortDirection}, GroupKey ASC
            LIMIT $limit OFFSET $offset;
            """;
        AddDateParameters(command, start, endExclusive);
        command.Parameters.AddWithValue("$limit", _pageSize);
        command.Parameters.AddWithValue("$offset", Math.Max(0, page) * _pageSize);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(new FileLogGroup(
                ReadText(reader, 0),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
        return result;
    }

    private List<LogViewEntry> ReadFileGroupEntries(DateTime start, DateTime endExclusive, string key)
    {
        var sql = $"""
            SELECT Id, CreatedAt, ActivityType, FullPath, FolderPath, FileName, Extension,
                   DateOpened, TimeOpened, SizeAtOpening, FirstWriteDate, FirstWriteTime,
                   LastWriteDate, LastWriteTime, WriteCount, SizeAtLastWrite, DateClosed,
                   TimeClosed, SizeAtClosing, InferredAction, ProcessName, ProcessId, Note,
                   Scope, WindowsSid, SessionId, ShowInCalendarView
            FROM ActivityEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end AND {FileGroupExpression()} = $groupKey
            {BuildOrderBy(_fileSortColumn, _fileSortAscending)};
            """;
        return ReadEntries(sql, start, endExclusive, 0, reader => CreateFileEntry(reader), false, key);
    }

    private LogViewEntry CreateFileEntry(SqliteDataReader reader)
    {
        var createdAt = ReadText(reader, 1);
        var fullPath = ReadText(reader, 3);
        var folder = ReadText(reader, 4);
        var file = ReadText(reader, 5);
        if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(fullPath) ?? "";
        if (string.IsNullOrWhiteSpace(file)) file = Path.GetFileName(fullPath);
        var fields = new Dictionary<string, string>
        {
            ["ID"] = ReadText(reader, 0), ["Created At"] = createdAt, ["Activity Type"] = ReadText(reader, 2),
            ["Full Path"] = fullPath, ["Folder"] = folder, ["File"] = file, ["Extension"] = ReadText(reader, 6),
            ["Date Opened"] = ReadText(reader, 7), ["Time Opened"] = ReadText(reader, 8), ["Size At Opening"] = ReadText(reader, 9),
            ["First Write Date"] = ReadText(reader, 10), ["First Write Time"] = ReadText(reader, 11),
            ["Last Write Date"] = ReadText(reader, 12), ["Last Write Time"] = ReadText(reader, 13),
            ["Write Count"] = ReadText(reader, 14), ["Size At Last Write"] = ReadText(reader, 15),
            ["Date Closed"] = ReadText(reader, 16), ["Time Closed"] = ReadText(reader, 17), ["Size At Closing"] = ReadText(reader, 18),
            ["Inferred Action"] = ReadText(reader, 19), ["Process"] = ReadText(reader, 20), ["Process ID"] = ReadText(reader, 21), ["Note"] = ReadText(reader, 22),
            ["Scope"] = ReadText(reader, 23), ["Windows SID"] = ReadText(reader, 24), ["Session ID"] = ReadText(reader, 25),
            ["Show in Calendar View"] = ReadCalendarFlag(reader, 26) ? "Yes" : "No"
        };
        return new LogViewEntry(ReadText(reader, 0), createdAt, EventDate(createdAt, ReadText(reader, 7), ReadText(reader, 10), ReadText(reader, 12), ReadText(reader, 16)),
            EventTime(createdAt, ReadText(reader, 8), ReadText(reader, 11), ReadText(reader, 13), ReadText(reader, 17)), file, folder, ReadText(reader, 20), ReadText(reader, 21), fullPath, ReadCalendarFlag(reader, 26), fields);
    }

    private static void AddDateParameters(SqliteCommand command, DateTime start, DateTime endExclusive)
    {
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endExclusive.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
    }

    private void PopulateFileGroups(IEnumerable<FileLogGroup> groups, DateTime start, DateTime endExclusive)
    {
        SetGroupedColumnVisibility(
            gridFile,
            FileGroupDisplayColumnName(),
            _expandedFileGroups.Count > 0);
        gridFile.Rows.Clear();
        foreach (var group in groups)
        {
            var expanded = _expandedFileGroups.Contains(group.Key);
            var rowIndex = gridFile.Rows.Add();
            var row = gridFile.Rows[rowIndex];
            row.Cells[FileGroupDisplayColumnName()].Value = $"{(expanded ? "▼" : "▶")} {group.Key}";
            row.Cells["RecordCount"].Value = group.Count;
            row.Cells["ShowInCalendarView"].Value = CalendarCheckState(group.MarkedCount, group.Count);
            row.Tag = group;
            row.DefaultCellStyle.Font = new System.Drawing.Font(gridFile.Font, System.Drawing.FontStyle.Bold);
            row.DefaultCellStyle.BackColor = System.Drawing.SystemColors.ControlLight;
            ConfigureGroupActionCell(row, expanded);
            if (!expanded) continue;
            foreach (var entry in ReadFileGroupEntries(start, endExclusive, group.Key))
            {
                var childIndex = gridFile.Rows.Add(entry.Id, entry.Date, entry.Time, "    " + entry.Subject,
                    entry.Fields.TryGetValue("Extension", out var ext) ? ext : "",
                    GetFileActivity(entry), entry.Folder, entry.App);
                gridFile.Rows[childIndex].Tag = entry;
                gridFile.Rows[childIndex].Cells["ShowInCalendarView"].Value =
                    entry.ShowInCalendarView ? CheckState.Checked : CheckState.Unchecked;
                ConfigureNonGroupSummaryCell(gridFile.Rows[childIndex]);
            }
        }
    }

    private void PopulateFileGrid(IEnumerable<LogViewEntry> entries)
    {
        RestoreDetailColumnVisibility(gridFile);
        gridFile.Rows.Clear();
        foreach (var e in entries) AddRow(gridFile, e, e.Id, e.Date, e.Time, e.Subject,
            e.Fields.TryGetValue("Extension", out var extension) ? extension : "",
            GetFileActivity(e), e.Folder, e.App);
    }

    private static string GetFileActivity(LogViewEntry entry)
    {
        if (entry.Fields.TryGetValue("Inferred Action", out var inferred) && !string.IsNullOrWhiteSpace(inferred))
            return inferred;
        if (entry.Fields.TryGetValue("Activity Type", out var activityType) && !string.IsNullOrWhiteSpace(activityType))
            return activityType;
        return "Unknown";
    }

    private void PopulateAppGrid(IEnumerable<LogViewEntry> entries)
    {
        RestoreDetailColumnVisibility(gridApp);
        gridApp.Rows.Clear();
        foreach (var e in entries) AddRow(gridApp, e, e.Id, e.Date, e.Time, e.App, e.ProcessId, e.Path);
    }

    private void PopulateAppGroups(IEnumerable<AppLogGroup> groups, DateTime start, DateTime endExclusive)
    {
        SetGroupedColumnVisibility(
            gridApp,
            AppGroupDisplayColumnName(),
            _expandedAppGroups.Count > 0);
        gridApp.Rows.Clear();
        foreach (var group in groups)
        {
            var expanded = _expandedAppGroups.Contains(group.Key);
            var rowIndex = gridApp.Rows.Add();
            var row = gridApp.Rows[rowIndex];
            row.Cells[AppGroupDisplayColumnName()].Value = $"{(expanded ? "▼" : "▶")} {group.Key}";
            row.Cells["RecordCount"].Value = group.Count;
            row.Cells["ShowInCalendarView"].Value = CalendarCheckState(group.MarkedCount, group.Count);
            row.Tag = group;
            row.DefaultCellStyle.Font = new System.Drawing.Font(gridApp.Font, System.Drawing.FontStyle.Bold);
            row.DefaultCellStyle.BackColor = System.Drawing.SystemColors.ControlLight;
            ConfigureGroupActionCell(row, expanded);
            if (!expanded)
                continue;

            foreach (var entry in ReadAppGroupEntries(start, endExclusive, group.Key))
            {
                var childIndex = gridApp.Rows.Add(
                    entry.Id,
                    entry.Date,
                    entry.Time,
                    "    " + entry.App,
                    entry.ProcessId,
                    entry.Path);
                gridApp.Rows[childIndex].Tag = entry;
                gridApp.Rows[childIndex].Cells["ShowInCalendarView"].Value =
                    entry.ShowInCalendarView ? CheckState.Checked : CheckState.Unchecked;
                ConfigureNonGroupSummaryCell(gridApp.Rows[childIndex]);
            }
        }
    }

    private string FileGroupDisplayColumnName() => _fileGroupBy switch
    {
        "Date" => "Date",
        "Extension" => "Extension",
        "Folder" => "Folder",
        "Application" => "App",
        "Activity" => "Activity",
        _ => "File"
    };

    private string FileGroupDisplayHeaderText() => _fileGroupBy switch
    {
        "File name" => "File",
        "Application" => "App",
        _ => _fileGroupBy
    };

    private string AppGroupDisplayColumnName() => _appGroupBy switch
    {
        "Date" => "Date",
        "Process ID" => "ProcessID",
        "Path" => "Path",
        _ => "App"
    };

    private string AppGroupDisplayHeaderText() => _appGroupBy switch
    {
        "Application" => "App",
        _ => _appGroupBy
    };

    private void PopulateUserGrid(IEnumerable<LogViewEntry> entries)
    {
        SetRecordCountColumnVisible(gridUser, false);
        gridUser.Rows.Clear();
        foreach (var e in entries) AddRow(gridUser, e, e.Id, e.Date, e.Time, e.Subject, e.App, e.Fields.TryGetValue("Computer", out var computer) ? computer : "");
    }

    private static void SetRecordCountColumnVisible(DataGridView grid, bool visible)
    {
        if (grid.Columns["RecordCount"] is { } column)
            column.Visible = visible;
    }

    private static void SetGroupedColumnVisibility(
        DataGridView grid,
        string groupColumnName,
        bool hasExpandedGroups)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column is DataGridViewButtonColumn)
                continue;

            column.Visible =
                column.Name == "RecordCount" ||
                column.Name == "ShowInCalendarView" ||
                hasExpandedGroups ||
                column.Name.Equals(groupColumnName, StringComparison.Ordinal);
        }
    }

    private static void RestoreDetailColumnVisibility(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column is not DataGridViewButtonColumn)
                column.Visible = column.Name != "RecordCount";
        }
    }

    private static void AddRow(DataGridView grid, LogViewEntry entry, params object[] values)
    {
        var rowIndex = grid.Rows.Add(values);
        grid.Rows[rowIndex].Tag = entry;
        grid.Rows[rowIndex].Cells["ShowInCalendarView"].Value =
            entry.ShowInCalendarView ? CheckState.Checked : CheckState.Unchecked;
        ConfigureNonGroupSummaryCell(grid.Rows[rowIndex]);
    }

    private static CheckState CalendarCheckState(int markedCount, int totalCount) =>
        markedCount <= 0
            ? CheckState.Unchecked
            : markedCount >= totalCount
                ? CheckState.Checked
                : CheckState.Indeterminate;

    private static void ConfigureGroupActionCell(DataGridViewRow row, bool expanded)
    {
        if (row.DataGridView?.Columns["Details"] is not { } detailsColumn)
            return;

        row.Cells[detailsColumn.Index] = new DataGridViewButtonCell
        {
            UseColumnTextForButtonValue = false,
            Value = expanded ? "Collapse" : "Expand"
        };

        if (row.DataGridView.Columns["Summary"] is { } summaryColumn)
        {
            row.Cells[summaryColumn.Index] = new DataGridViewButtonCell
            {
                UseColumnTextForButtonValue = false,
                Value = "Summary"
            };
        }
    }

    private static void ConfigureNonGroupSummaryCell(DataGridViewRow row)
    {
        if (row.DataGridView?.Columns["Summary"] is not { } summaryColumn)
            return;
        row.Cells[summaryColumn.Index] = new DataGridViewTextBoxCell();
    }

    private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (sender is DataGridView grid &&
            grid.IsCurrentCellDirty &&
            grid.CurrentCell?.OwningColumn.Name == "ShowInCalendarView")
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private async void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_updatingCalendarCells ||
            sender is not DataGridView grid ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            grid.Columns[e.ColumnIndex].Name != "ShowInCalendarView")
        {
            return;
        }

        var row = grid.Rows[e.RowIndex];
        var showInCalendar = row.Cells[e.ColumnIndex].Value switch
        {
            CheckState.Checked => true,
            bool value => value,
            _ => false
        };

        try
        {
            string tableName;
            IReadOnlyList<long> ids;
            if (row.Tag is LogViewEntry entry)
            {
                tableName = GetTableName(grid);
                ids = new[] { long.Parse(entry.Id, CultureInfo.InvariantCulture) };
            }
            else if (grid == gridFile && row.Tag is FileLogGroup fileGroup)
            {
                tableName = "ActivityEvents";
                ids = ReadGroupRecordIds(
                    tableName,
                    FileGroupExpression(),
                    fileGroup.Key,
                    dateStart.Value,
                    dateEnd.Value);
            }
            else if (grid == gridApp && row.Tag is AppLogGroup appGroup)
            {
                tableName = "ProgramEvents";
                ids = ReadGroupRecordIds(
                    tableName,
                    AppGroupExpression(),
                    appGroup.Key,
                    dateStart.Value,
                    dateEnd.Value);
            }
            else
            {
                return;
            }

            Cursor = Cursors.WaitCursor;
            grid.Enabled = false;
            await ServicePipeClient.SetCalendarVisibilityAsync(
                tableName,
                ids,
                showInCalendar,
                systemDatabase: _systemOnly);
            statusLabel.Text = showInCalendar
                ? "Added to Calendar View."
                : "Removed from Calendar View.";
            RefreshActiveTab();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "DeskPulse could not update Calendar View.\n\n" + ex.Message,
                "Calendar View",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            RefreshActiveTab();
        }
        finally
        {
            grid.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private string GetTableName(DataGridView grid) =>
        grid == gridFile ? "ActivityEvents" :
        grid == gridApp ? "ProgramEvents" :
        grid == gridUser ? "UserEvents" :
        throw new ArgumentOutOfRangeException(nameof(grid));

    private void CalendarViewButton_Click(object? sender, EventArgs e)
    {
        using var calendar = new CalendarViewForm(
            _databaseFilePath,
            _systemOnly
                ? "DeskPulse - System Calendar View"
                : "DeskPulse - Calendar View");
        calendar.ShowDialog(this);
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var row = grid.Rows[e.RowIndex];
        if (grid.Columns[e.ColumnIndex].Name == "Summary")
        {
            ShowGroupSummary(grid, row);
            return;
        }

        if (grid.Columns[e.ColumnIndex].Name != "Details")
            return;

        if (ToggleGroupRow(grid, row))
            RefreshActiveTab();
        else
            ShowDetails(row);
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0) return;
        var row = grid.Rows[e.RowIndex];
        if (ToggleGroupRow(grid, row))
        {
            RefreshActiveTab();
            return;
        }
        ShowDetails(row);
    }

    private bool ToggleGroupRow(DataGridView grid, DataGridViewRow row)
    {
        if (grid == gridFile && row.Tag is FileLogGroup group)
        {
            if (!_expandedFileGroups.Add(group.Key))
                _expandedFileGroups.Remove(group.Key);
            return true;
        }

        if (grid == gridApp && row.Tag is AppLogGroup appGroup)
        {
            if (!_expandedAppGroups.Add(appGroup.Key))
                _expandedAppGroups.Remove(appGroup.Key);
            return true;
        }

        return false;
    }

    private void ShowGroupSummary(DataGridView grid, DataGridViewRow row)
    {
        IReadOnlyDictionary<string, string>? fields = row.Tag switch
        {
            FileLogGroup fileGroup when grid == gridFile => ReadFileGroupSummary(
                fileGroup.Key, dateStart.Value, dateEnd.Value),
            AppLogGroup appGroup when grid == gridApp => ReadAppGroupSummary(
                appGroup.Key, dateStart.Value, dateEnd.Value),
            _ => null
        };

        if (fields == null)
            return;

        using var details = new LogEntryDetailsForm(fields);
        details.Text = "DeskPulse - Group Summary";
        details.ShowDialog(this);
    }

    private IReadOnlyDictionary<string, string> ReadFileGroupSummary(
        string groupKey,
        DateTime start,
        DateTime endExclusive)
    {
        var fields = new Dictionary<string, string>
        {
            ["Grouped by"] = _fileGroupBy,
            ["Group"] = groupKey,
            ["Selected period"] = $"{start:dd/MM/yyyy HH:mm:ss} — {endExclusive:dd/MM/yyyy HH:mm:ss}"
        };

        using var connection = OpenReadConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT COUNT(*), MIN(CreatedAt), MAX(CreatedAt),
                       COUNT(DISTINCT NULLIF(FullPath, '')),
                       COUNT(DISTINCT NULLIF(FolderPath, '')),
                       COUNT(DISTINCT NULLIF(Extension, '')),
                       COUNT(DISTINCT NULLIF(ProcessName, '')),
                       SUM(CASE WHEN WriteCount GLOB '[0-9]*' THEN CAST(WriteCount AS INTEGER) ELSE 0 END)
                FROM ActivityEvents
                WHERE CreatedAt >= $start AND CreatedAt < $end
                  AND {FileGroupExpression()} = $groupKey;
                """;
            AddDateParameters(command, start, endExclusive);
            command.Parameters.AddWithValue("$groupKey", groupKey);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                fields["Total records"] = ReadText(reader, 0);
                fields["Oldest activity"] = ReadText(reader, 1);
                fields["Newest activity"] = ReadText(reader, 2);
                fields["Unique full paths"] = ReadText(reader, 3);
                fields["Unique folders"] = ReadText(reader, 4);
                fields["Unique extensions"] = ReadText(reader, 5);
                fields["Unique applications"] = ReadText(reader, 6);
                fields["Total recorded writes"] = ReadText(reader, 7);
            }
        }

        fields["Top activities"] = ReadTopGroupValues(
            connection, "ActivityEvents", FileGroupExpression(),
            "COALESCE(NULLIF(InferredAction, ''), NULLIF(ActivityType, ''), '(unknown activity)')",
            groupKey, start, endExclusive);
        fields["Top applications"] = ReadTopGroupValues(
            connection, "ActivityEvents", FileGroupExpression(),
            "COALESCE(NULLIF(ProcessName, ''), '(unknown application)')",
            groupKey, start, endExclusive);
        fields["Top paths"] = ReadTopGroupValues(
            connection, "ActivityEvents", FileGroupExpression(),
            "COALESCE(NULLIF(FullPath, ''), '(unknown path)')",
            groupKey, start, endExclusive);
        return fields;
    }

    private IReadOnlyDictionary<string, string> ReadAppGroupSummary(
        string groupKey,
        DateTime start,
        DateTime endExclusive)
    {
        var fields = new Dictionary<string, string>
        {
            ["Grouped by"] = _appGroupBy,
            ["Group"] = groupKey,
            ["Selected period"] = $"{start:dd/MM/yyyy HH:mm:ss} — {endExclusive:dd/MM/yyyy HH:mm:ss}"
        };

        using var connection = OpenReadConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT COUNT(*), MIN(CreatedAt), MAX(CreatedAt),
                       COUNT(DISTINCT NULLIF(ProgramName, '')),
                       COUNT(DISTINCT NULLIF(FilePath, '')),
                       COUNT(DISTINCT ProcessId),
                       COUNT(DISTINCT NULLIF(WindowTitle, ''))
                FROM ProgramEvents
                WHERE CreatedAt >= $start AND CreatedAt < $end
                  AND {AppGroupExpression()} = $groupKey;
                """;
            AddDateParameters(command, start, endExclusive);
            command.Parameters.AddWithValue("$groupKey", groupKey);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                fields["Total records"] = ReadText(reader, 0);
                fields["Oldest activity"] = ReadText(reader, 1);
                fields["Newest activity"] = ReadText(reader, 2);
                fields["Unique applications"] = ReadText(reader, 3);
                fields["Unique executable paths"] = ReadText(reader, 4);
                fields["Unique process IDs"] = ReadText(reader, 5);
                fields["Unique window titles"] = ReadText(reader, 6);
            }
        }

        fields["Top applications"] = ReadTopGroupValues(
            connection, "ProgramEvents", AppGroupExpression(),
            "COALESCE(NULLIF(ProgramName, ''), '(unknown application)')",
            groupKey, start, endExclusive);
        fields["Top executable paths"] = ReadTopGroupValues(
            connection, "ProgramEvents", AppGroupExpression(),
            "COALESCE(NULLIF(FilePath, ''), '(unknown path)')",
            groupKey, start, endExclusive);
        fields["Top event descriptions"] = ReadTopGroupValues(
            connection, "ProgramEvents", AppGroupExpression(),
            "COALESCE(NULLIF(EventDescription, ''), '(unknown event)')",
            groupKey, start, endExclusive);
        return fields;
    }

    private static string ReadTopGroupValues(
        SqliteConnection connection,
        string table,
        string groupExpression,
        string valueExpression,
        string groupKey,
        DateTime start,
        DateTime endExclusive)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {valueExpression} AS Value, COUNT(*) AS RecordCount
            FROM {table}
            WHERE CreatedAt >= $start AND CreatedAt < $end
              AND {groupExpression} = $groupKey
            GROUP BY Value
            ORDER BY RecordCount DESC, Value ASC
            LIMIT 5;
            """;
        AddDateParameters(command, start, endExclusive);
        command.Parameters.AddWithValue("$groupKey", groupKey);
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add($"{ReadText(reader, 0)} ({ReadText(reader, 1)})");
        return values.Count == 0 ? "(none)" : string.Join(Environment.NewLine, values);
    }

    private static void ShowDetails(DataGridViewRow row)
    {
        if (row.Tag is not LogViewEntry entry) return;
        using var details = new LogEntryDetailsForm(entry.Fields);
        details.ShowDialog(row.DataGridView?.FindForm());
    }
}

public sealed record FileLogGroup(string Key, int Count, int MarkedCount);
public sealed record AppLogGroup(string Key, int Count, int MarkedCount);
public readonly record struct GroupRuleSuggestion(
    string Value,
    LogRuleCategory FormCategory,
    string RuleType,
    bool IncludeSubfolders);

public readonly record struct RuleSuggestion(
    string Value,
    LogRuleCategory FormCategory,
    string RuleType,
    bool IncludeSubfolders);

public sealed record LogViewEntry(
    string Id,
    string CreatedAt,
    string Date,
    string Time,
    string Subject,
    string Folder,
    string App,
    string ProcessId,
    string Path,
    bool ShowInCalendarView,
    IReadOnlyDictionary<string, string> Fields);
