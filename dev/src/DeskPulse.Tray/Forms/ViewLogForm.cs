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
    private bool _use12HourTime;
    private readonly HashSet<string> _expandedFileGroups = new(StringComparer.Ordinal);

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
    private string _fileSortColumn = "CreatedAt";
    private bool _fileSortAscending;
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
            Text = "DeskPulse - System Log (read-only)";
            createRuleButton.Visible = false;
            deleteButton.Visible = false;
        }

        _pageSize = LoadPageSize();
        _use12HourTime = LoadUse12HourTime();
        timeFormatCombo.SelectedItem = _use12HourTime ? "12-hour" : "24-hour";
        pageSizeInput.Value = Math.Clamp(_pageSize, (int)pageSizeInput.Minimum, (int)pageSizeInput.Maximum);
        dateStart.Value = GetFirstRecordedDate();
        dateEnd.Value = DateTime.Today;
        ConfigureGrids();
        tabs.SelectedIndexChanged += (_, _) =>
        {
            UpdateSelectionButtons();
            UpdatePagingControls();
            groupByLabel.Visible = groupByCombo.Visible = tabs.SelectedTab?.Text == "File Activity";
            UpdatePageStatus();
        };
        Load += (_, _) =>
        {
            groupByLabel.Visible = groupByCombo.Visible = tabs.SelectedTab?.Text == "File Activity";
            RefreshLog();
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

    private void TodayOnlyButton_Click(object? sender, EventArgs e)
    {
        dateStart.Value = DateTime.Today;
        _filePage = 0;
        _appPage = 0;
        _userPage = 0;
        RefreshLog();
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
        grid.ReadOnly = true;
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
                SortMode = DataGridViewColumnSortMode.Programmatic,
                FillWeight = columnName is "Folder" or "Path" ? 180 : columnName is "File" or "App" or "Event" ? 130 : columnName == "Extension" ? 65 : columnName == "ID" ? 55 : 80
            });
        }

        grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Details",
            HeaderText = "",
            Text = "Details",
            UseColumnTextForButtonValue = true,
            Width = 72,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        grid.CellContentClick += Grid_CellContentClick;
        grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        grid.CellDoubleClick += Grid_CellDoubleClick;
        grid.SelectionChanged += (_, _) => UpdateSelectionButtons();
    }



    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.ColumnIndex < 0)
            return;

        var column = grid.Columns[e.ColumnIndex];
        if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
            return;

        var databaseColumn = GetDatabaseSortColumn(grid, column.HeaderText);
        if (databaseColumn == null)
            return;

        if (grid == gridApp)
        {
            _appSortAscending = string.Equals(_appSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                ? !_appSortAscending
                : true;
            _appSortColumn = databaseColumn;
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
            _fileSortAscending = string.Equals(_fileSortColumn, databaseColumn, StringComparison.OrdinalIgnoreCase)
                ? !_fileSortAscending
                : true;
            _fileSortColumn = databaseColumn;
            _filePage = 0;
        }

        UpdateSortGlyphs(grid, column, GetSortAscending(grid));
        RefreshActiveTab();
    }

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
            column.HeaderCell.SortGlyphDirection = SortOrder.None;

        sortedColumn.HeaderCell.SortGlyphDirection = ascending ? SortOrder.Ascending : SortOrder.Descending;
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
        createRuleButton.Enabled = selectedCount == 1 && grid!.SelectedRows[0].Tag is LogViewEntry;
        deleteButton.Enabled = selectedCount > 0 && grid!.SelectedRows.Cast<DataGridViewRow>().Any(row => row.Tag is LogViewEntry);
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
        var grid = GetActiveGrid();
        if (grid == null)
            return;

        var entries = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Tag as LogViewEntry)
            .Where(entry => entry != null && long.TryParse(entry.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Cast<LogViewEntry>()
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (entries.Count == 0)
            return;

        var sectionName = tabs.SelectedTab?.Text ?? "activity";
        using var deleteForm = new DeleteLogRecordsForm(entries.Count, sectionName);
        if (deleteForm.ShowDialog(this) != DialogResult.OK)
            return;

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
            statusLabel.Text = $"Deleting {entries.Count:N0} selected record(s)...";
            var ids = entries
                .Select(entry => long.Parse(entry.Id, CultureInfo.InvariantCulture))
                .ToArray();

            var deleted = await ServicePipeClient.DeleteRecordsAsync(tableName, ids);
            var rulesCreated = createRules ? CreateExclusionRulesForEntries(entries) : 0;
            RefreshActiveTab();
            statusLabel.Text = rulesCreated > 0
                ? $"Deleted {deleted:N0} selected record(s) and created {rulesCreated:N0} exclusion rule(s)."
                : $"Deleted {deleted:N0} selected record(s).";
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


    private int CreateExclusionRulesForEntries(IReadOnlyList<LogViewEntry> entries)
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
            var value = category switch
            {
                LogRuleCategory.App => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.App,
                LogRuleCategory.File => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.Subject,
                _ => entry.Subject
            };

            value = value?.Trim() ?? string.Empty;
            if (value.Length == 0)
                continue;

            var rule = new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = category switch
                {
                    LogRuleCategory.App => "process",
                    LogRuleCategory.File => "file",
                    _ => "event"
                },
                Action = "Exclude",
                Value = value,
                IncludeSubfolders = false
            };

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

    private void CreateRuleButton_Click(object? sender, EventArgs e)
    {
        var grid = GetActiveGrid();
        if (grid?.SelectedRows.Count != 1 || grid.SelectedRows[0].Tag is not LogViewEntry entry)
            return;

        var category = GetActiveCategory();
        var suggestedValue = category switch
        {
            LogRuleCategory.App => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.App,
            LogRuleCategory.File => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.Subject,
            _ => entry.Subject
        };

        using var form = new AddLogRuleForm(category, suggestedValue);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        var settings = AppSettings.Load();
        var rule = new ActivityRuleSetting
        {
            Enabled = form.RuleEnabled,
            RuleType = category switch
            {
                LogRuleCategory.App => "process",
                LogRuleCategory.File => "file",
                _ => "event"
            },
            Action = form.IsInclude ? "Include" : "Exclude",
            Value = form.RuleValue,
            IncludeSubfolders = false
        };

        var target = category switch
        {
            LogRuleCategory.App => settings.AppActivityRuleSettings,
            LogRuleCategory.File => settings.FileActivityRuleSettings,
            _ => settings.UserActivityRuleSettings
        };

        var duplicate = target.Any(existing =>
            existing.RuleType.Equals(rule.RuleType, StringComparison.OrdinalIgnoreCase) &&
            existing.Value.Equals(rule.Value, StringComparison.OrdinalIgnoreCase) &&
            existing.Action.Equals(rule.Action, StringComparison.OrdinalIgnoreCase) &&
            existing.IncludeSubfolders == rule.IncludeSubfolders);

        if (duplicate)
        {
            MessageBox.Show(this, "An equivalent rule already exists in this rules list.", "Add rule to rules list", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var conflictingIds = form.CleanUpOldData && !form.IsInclude
            ? FindConflictingRecordIds(category, rule)
            : new ConflictingRecordIds();

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

        // Specific rules must precede broad catch-all rules because the first match wins.
        target.Insert(0, rule);

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
                    var result = ServicePipeClient.RunDatabaseHousekeepingAsync().GetAwaiter().GetResult();
                    progress.Report(new ExportProgressInfo(98, "98%  Finalising cleaned database"));
                    return result;
                });

            if (progressForm.ShowDialog(this) != DialogResult.OK)
                return;
        }
        else
        {
            settings.Save();
        }

        _settingsChanged?.Invoke();
        RefreshLog();
        MessageBox.Show(this, "The rule was added successfully.", "Add rule to rules list", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private ConflictingRecordIds FindConflictingRecordIds(LogRuleCategory category, ActivityRuleSetting rule)
    {
        var result = new ConflictingRecordIds();
        using var connection = OpenReadConnection();
        using (var busy = connection.CreateCommand()) { busy.CommandText = "PRAGMA busy_timeout=5000;"; busy.ExecuteNonQuery(); }

        if (category == LogRuleCategory.File)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, COALESCE(FullPath, '') FROM ActivityEvents;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var fullPath = reader.GetString(1);
                if (FilePatternMatches(fullPath, rule.Value)) result.ActivityIds.Add(id);
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
        pattern = (pattern ?? "").Trim().Trim('"');
        if (Path.IsPathRooted(pattern) && !ContainsWildcard(pattern))
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


    private async void ExportButton_Click(object? sender, EventArgs e)
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

        try
        {
            Cursor = Cursors.WaitCursor;
            var start = dateStart.Value.Date;
            var endExclusive = dateEnd.Value.Date.AddDays(1);
            var exportedColumns = grid.Columns
                .Cast<DataGridViewColumn>()
                .Where(column => column.Visible && column is not DataGridViewButtonColumn)
                .OrderBy(column => column.DisplayIndex)
                .Select(column => column.HeaderText)
                .ToList();

            SetLogExportInProgress(true);
            var progress = new Progress<ExportProgressInfo>(UpdateLogExportProgress);
            var exportedCount = await Task.Run(() => ExportCompleteLog(
                dialog.FileName,
                sectionName,
                exportedColumns,
                start,
                endExclusive,
                progress));

            UpdateLogExportProgress(new ExportProgressInfo(100, $"Exported all {exportedCount:N0} record(s) from {sectionName}."));
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
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
        exportProgressBar.Visible = inProgress;
        if (inProgress)
            exportProgressBar.Value = 0;
    }

    private void UpdateLogExportProgress(ExportProgressInfo progress)
    {
        exportProgressBar.Value = Math.Clamp(progress.Percent, exportProgressBar.Minimum, exportProgressBar.Maximum);
        statusLabel.Text = progress.Message;
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
        _fileGroupBy = groupByCombo.SelectedItem?.ToString() ?? "None";
        _expandedFileGroups.Clear();
        _filePage = 0;
        if (IsHandleCreated) RefreshActiveTab();
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
        pageLabel.Text = tabs.SelectedTab?.Text == "File Activity" && _fileGroupBy != "None"
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
        statusLabel.Text = tabs.SelectedTab?.Text == "File Activity" && _fileGroupBy != "None"
            ? $"Showing groups {firstRecord:N0} to {lastRecord:N0} of {total:N0}. Double-click a group to expand or collapse it."
            : $"Showing {firstRecord:N0} to {lastRecord:N0} of {total:N0} records.";
    }

    private void RefreshActiveTab()
    {
        var start = dateStart.Value.Date;
        var end = dateEnd.Value.Date;
        if (end < start) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Reading log page...";
            Application.DoEvents();
            var endExclusive = end.AddDays(1);

            switch (tabs.SelectedTab?.Text)
            {
                case "App Activity":
                    _appTotal = CountEntries("ProgramEvents", start, endExclusive);
                    _appPage = ClampPage(_appPage, _appTotal);
                    PopulateAppGrid(ReadAppEntries(start, endExclusive, _appPage));
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
        var start = dateStart.Value.Date;
        var end = dateEnd.Value.Date;

        if (end < start)
        {
            MessageBox.Show(this, "The end date cannot be before the start date.", "View Log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Reading log...";
            Application.DoEvents();

            var endExclusive = end.AddDays(1);
            _fileTotal = _fileGroupBy == "None" ? CountEntries("ActivityEvents", start, endExclusive) : CountFileGroups(start, endExclusive);
            _appTotal = CountEntries("ProgramEvents", start, endExclusive);
            _userTotal = CountEntries("UserEvents", start, endExclusive);
            ClampAllPages();

            var fileEntries = _fileGroupBy == "None" ? ReadFileEntries(start, endExclusive, _filePage) : new List<LogViewEntry>();
            var fileGroups = _fileGroupBy == "None" ? new List<FileLogGroup>() : ReadFileGroups(start, endExclusive, _filePage);
            var appEntries = ReadAppEntries(start, endExclusive, _appPage);
            var userEntries = ReadUserEntries(start, endExclusive, _userPage);

            if (_fileGroupBy == "None") PopulateFileGrid(fileEntries); else PopulateFileGroups(fileGroups, start, endExclusive);
            PopulateAppGrid(appEntries);
            PopulateUserGrid(userEntries);

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
                   Scope, WindowsSid, SessionId
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
                ["Scope"] = ReadText(reader, 23), ["Windows SID"] = ReadText(reader, 24), ["Session ID"] = ReadText(reader, 25)
            };

            return new LogViewEntry(ReadText(reader, 0), createdAt, EventDate(createdAt, ReadText(reader, 7), ReadText(reader, 10), ReadText(reader, 12), ReadText(reader, 16)),
                EventTime(createdAt, ReadText(reader, 8), ReadText(reader, 11), ReadText(reader, 13), ReadText(reader, 17)),
                file, folder, ReadText(reader, 20), ReadText(reader, 21), fullPath, fields);
        }, usePaging);
    }

    private List<LogViewEntry> ReadAppEntries(DateTime start, DateTime endExclusive, int page, bool usePaging = true)
    {
        var pagingClause = usePaging ? "LIMIT $limit OFFSET $offset;" : "";
        var sql = $"""
            SELECT Id, CreatedAt, EventDate, EventTime, EventDescription, ProgramName,
                   ProcessId, FilePath, WindowTitle, UserName, MachineName, AppVersion, Note,
                   Scope, WindowsSid, SessionId
            FROM ProgramEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            {BuildOrderBy(_appSortColumn, _appSortAscending)}
            {pagingClause}
            """;

        return ReadEntries(sql, start, endExclusive, page, reader =>
        {
            var fields = new Dictionary<string, string>
            {
                ["ID"] = ReadText(reader, 0), ["Created At"] = ReadText(reader, 1), ["Date"] = ReadText(reader, 2), ["Time"] = ReadText(reader, 3),
                ["Event"] = ReadText(reader, 4), ["App"] = ReadText(reader, 5), ["Process ID"] = ReadText(reader, 6),
                ["App Path"] = ReadText(reader, 7), ["Window Title"] = ReadText(reader, 8), ["User"] = ReadText(reader, 9),
                ["Computer"] = ReadText(reader, 10), ["DeskPulse Version"] = ReadText(reader, 11), ["Note"] = ReadText(reader, 12),
                ["Scope"] = ReadText(reader, 13), ["Windows SID"] = ReadText(reader, 14), ["Session ID"] = ReadText(reader, 15)
            };
            return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), FormatDisplayTime(ReadText(reader, 3)), ReadText(reader, 5), "", ReadText(reader, 5), ReadText(reader, 6), ReadText(reader, 7), fields);
        }, usePaging);
    }

    private List<LogViewEntry> ReadUserEntries(DateTime start, DateTime endExclusive, int page, bool usePaging = true)
    {
        var pagingClause = usePaging ? "LIMIT $limit OFFSET $offset;" : "";
        var sql = $"""
            SELECT Id, CreatedAt, EventDate, EventTime, EventDescription, UserName,
                   MachineName, ProcessName, ProcessId, AppVersion, Note,
                   Scope, WindowsSid, SessionId
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
                ["Scope"] = ReadText(reader, 11), ["Windows SID"] = ReadText(reader, 12), ["Session ID"] = ReadText(reader, 13)
            };
            return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), FormatDisplayTime(ReadText(reader, 3)), ReadText(reader, 4), "", ReadText(reader, 5), ReadText(reader, 8), "", fields);
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
        command.CommandText = $"SELECT {FileGroupExpression()} AS GroupKey, COUNT(*) AS RecordCount, MAX(CreatedAt) AS Latest FROM ActivityEvents WHERE CreatedAt >= $start AND CreatedAt < $end GROUP BY GroupKey ORDER BY Latest DESC, GroupKey ASC LIMIT $limit OFFSET $offset;";
        AddDateParameters(command, start, endExclusive);
        command.Parameters.AddWithValue("$limit", _pageSize);
        command.Parameters.AddWithValue("$offset", Math.Max(0, page) * _pageSize);
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new FileLogGroup(ReadText(reader, 0), Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
        return result;
    }

    private List<LogViewEntry> ReadFileGroupEntries(DateTime start, DateTime endExclusive, string key)
    {
        var sql = $"""
            SELECT Id, CreatedAt, ActivityType, FullPath, FolderPath, FileName, Extension,
                   DateOpened, TimeOpened, SizeAtOpening, FirstWriteDate, FirstWriteTime,
                   LastWriteDate, LastWriteTime, WriteCount, SizeAtLastWrite, DateClosed,
                   TimeClosed, SizeAtClosing, InferredAction, ProcessName, ProcessId, Note,
                   Scope, WindowsSid, SessionId
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
            ["Scope"] = ReadText(reader, 23), ["Windows SID"] = ReadText(reader, 24), ["Session ID"] = ReadText(reader, 25)
        };
        return new LogViewEntry(ReadText(reader, 0), createdAt, EventDate(createdAt, ReadText(reader, 7), ReadText(reader, 10), ReadText(reader, 12), ReadText(reader, 16)),
            EventTime(createdAt, ReadText(reader, 8), ReadText(reader, 11), ReadText(reader, 13), ReadText(reader, 17)), file, folder, ReadText(reader, 20), ReadText(reader, 21), fullPath, fields);
    }

    private static void AddDateParameters(SqliteCommand command, DateTime start, DateTime endExclusive)
    {
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endExclusive.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
    }

    private void PopulateFileGroups(IEnumerable<FileLogGroup> groups, DateTime start, DateTime endExclusive)
    {
        gridFile.Rows.Clear();
        foreach (var group in groups)
        {
            var expanded = _expandedFileGroups.Contains(group.Key);
            var rowIndex = gridFile.Rows.Add("", "", "", $"{(expanded ? "▼" : "▶")} {group.Key}", $"{group.Count:N0} records", "", "", "", "");
            var row = gridFile.Rows[rowIndex];
            row.Tag = group;
            row.DefaultCellStyle.Font = new System.Drawing.Font(gridFile.Font, System.Drawing.FontStyle.Bold);
            row.DefaultCellStyle.BackColor = System.Drawing.SystemColors.ControlLight;
            if (!expanded) continue;
            foreach (var entry in ReadFileGroupEntries(start, endExclusive, group.Key))
            {
                var childIndex = gridFile.Rows.Add(entry.Id, entry.Date, entry.Time, "    " + entry.Subject,
                    entry.Fields.TryGetValue("Extension", out var ext) ? ext : "",
                    GetFileActivity(entry), entry.Folder, entry.App, "Details");
                gridFile.Rows[childIndex].Tag = entry;
            }
        }
    }

    private void PopulateFileGrid(IEnumerable<LogViewEntry> entries)
    {
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
        gridApp.Rows.Clear();
        foreach (var e in entries) AddRow(gridApp, e, e.Id, e.Date, e.Time, e.App, e.ProcessId, e.Path);
    }

    private void PopulateUserGrid(IEnumerable<LogViewEntry> entries)
    {
        gridUser.Rows.Clear();
        foreach (var e in entries) AddRow(gridUser, e, e.Id, e.Date, e.Time, e.Subject, e.App, e.Fields.TryGetValue("Computer", out var computer) ? computer : "");
    }

    private static void AddRow(DataGridView grid, LogViewEntry entry, params object[] values)
    {
        var rowIndex = grid.Rows.Add(values);
        grid.Rows[rowIndex].Tag = entry;
    }

    private static void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Details") return;
        ShowDetails(grid.Rows[e.RowIndex]);
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0) return;
        var row = grid.Rows[e.RowIndex];
        if (grid == gridFile && row.Tag is FileLogGroup group)
        {
            if (!_expandedFileGroups.Add(group.Key)) _expandedFileGroups.Remove(group.Key);
            RefreshActiveTab();
            return;
        }
        ShowDetails(row);
    }

    private static void ShowDetails(DataGridViewRow row)
    {
        if (row.Tag is not LogViewEntry entry) return;
        using var details = new LogEntryDetailsForm(entry.Fields);
        details.ShowDialog(row.DataGridView?.FindForm());
    }
}

public sealed record FileLogGroup(string Key, int Count);

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
    IReadOnlyDictionary<string, string> Fields);
