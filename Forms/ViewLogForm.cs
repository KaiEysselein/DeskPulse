#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace DeskPulse;

public partial class ViewLogForm : Form
{
    private const int PageSize = 500;

    private int _folderPage;
    private int _appPage;
    private int _filePage;
    private int _userPage;
    private int _folderTotal;
    private int _appTotal;
    private int _fileTotal;
    private int _userTotal;
    private readonly string _connectionString;
    private readonly string _databaseFilePath;
    private readonly Action? _settingsChanged;

    public ViewLogForm(Action? settingsChanged = null)
    {
        InitializeComponent();

        _settingsChanged = settingsChanged;
        var settings = AppSettings.Load();
        _databaseFilePath = settings.DatabaseFilePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        dateStart.Value = DateTime.Today;
        dateEnd.Value = DateTime.Today;
        ConfigureGrids();
        tabs.SelectedIndexChanged += (_, _) =>
        {
            UpdateCreateRuleButton();
            UpdatePagingControls();
        };
        Load += (_, _) => RefreshLog();
    }

    private void ConfigureGrids()
    {
        ConfigureGrid(gridFolder, new[] { "ID", "Date", "Time", "Folder", "File", "Event", "App" });
        ConfigureGrid(gridApp, new[] { "ID", "Date", "Time", "Event Type", "App", "Process ID", "Path" });
        ConfigureGrid(gridFile, new[] { "ID", "Date", "Time", "Event Type", "File", "Folder", "App" });
        ConfigureGrid(gridUser, new[] { "ID", "Date", "Time", "Event Type", "Event", "User", "Computer" });
    }

    private void ConfigureGrid(DataGridView grid, IReadOnlyList<string> columns)
    {
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToOrderColumns = true;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        foreach (var columnName in columns)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = columnName.Replace(" ", ""),
                HeaderText = columnName,
                SortMode = DataGridViewColumnSortMode.Automatic,
                FillWeight = columnName is "Folder" or "Path" ? 180 : columnName is "File" or "App" or "Event" ? 130 : columnName == "ID" ? 55 : 80
            });
        }

        grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "More",
            HeaderText = "",
            Text = "More...",
            UseColumnTextForButtonValue = true,
            Width = 72,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        grid.CellContentClick += Grid_CellContentClick;
        grid.CellDoubleClick += Grid_CellDoubleClick;
        grid.SelectionChanged += (_, _) => UpdateCreateRuleButton();
    }


    private void UpdateCreateRuleButton()
    {
        var grid = GetActiveGrid();
        createRuleButton.Enabled = grid?.SelectedRows.Count == 1 && grid.SelectedRows[0].Tag is LogViewEntry;
    }

    private DataGridView? GetActiveGrid()
    {
        if (tabs.SelectedTab == null) return null;
        if (tabs.SelectedTab.Text == "Folder Activity") return gridFolder;
        if (tabs.SelectedTab.Text == "App Activity") return gridApp;
        if (tabs.SelectedTab.Text == "File Activity") return gridFile;
        if (tabs.SelectedTab.Text == "User Activity") return gridUser;
        return null;
    }

    private LogRuleCategory GetActiveCategory()
    {
        if (tabs.SelectedTab?.Text == "Folder Activity") return LogRuleCategory.Folder;
        if (tabs.SelectedTab?.Text == "App Activity") return LogRuleCategory.App;
        if (tabs.SelectedTab?.Text == "User Activity") return LogRuleCategory.User;
        return LogRuleCategory.File;
    }

    private void CreateRuleButton_Click(object? sender, EventArgs e)
    {
        var grid = GetActiveGrid();
        if (grid?.SelectedRows.Count != 1 || grid.SelectedRows[0].Tag is not LogViewEntry entry)
            return;

        var category = GetActiveCategory();
        var suggestedValue = category switch
        {
            LogRuleCategory.Folder => entry.Folder,
            LogRuleCategory.App => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.App,
            LogRuleCategory.File => !string.IsNullOrWhiteSpace(entry.Path) ? entry.Path : entry.Subject,
            _ => entry.EventType
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
                LogRuleCategory.Folder => "folder",
                LogRuleCategory.App => "process",
                LogRuleCategory.File => "file",
                _ => "event"
            },
            Action = form.IsInclude ? "Include" : "Exclude",
            Value = form.RuleValue,
            IncludeSubfolders = form.IncludeSubfolders
        };

        var target = category switch
        {
            LogRuleCategory.Folder => settings.FolderActivityRuleSettings,
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

                    progress.Report(new ExportProgressInfo(5, "5%   Preparing database cleanup"));
                    return DeleteRecordsAndCompact(conflictingIds, progress);
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
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databaseFilePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        using (var busy = connection.CreateCommand()) { busy.CommandText = "PRAGMA busy_timeout=5000;"; busy.ExecuteNonQuery(); }

        if (category is LogRuleCategory.File or LogRuleCategory.Folder)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, COALESCE(FullPath, ''), COALESCE(FolderPath, '') FROM ActivityEvents;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var fullPath = reader.GetString(1);
                var folder = reader.GetString(2);
                var matches = category == LogRuleCategory.File
                    ? FilePatternMatches(fullPath, rule.Value)
                    : FolderPatternMatches(fullPath, folder, rule.Value, rule.IncludeSubfolders);
                if (matches) result.ActivityIds.Add(id);
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
            command.CommandText = "SELECT Id, COALESCE(EventType, ''), COALESCE(EventDescription, '') FROM UserEvents;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var eventType = reader.GetString(1);
                var description = reader.GetString(2);
                if (TextPatternMatches(eventType, rule.Value) || TextPatternMatches(description, rule.Value)) result.UserIds.Add(reader.GetInt64(0));
            }
        }

        return result;
    }

    private MaintenanceExclusionCleanupResult DeleteRecordsAndCompact(
        ConflictingRecordIds ids,
        IProgress<ExportProgressInfo> progress)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databaseFilePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        using (var busy = connection.CreateCommand())
        {
            busy.CommandText = "PRAGMA busy_timeout=5000;";
            busy.ExecuteNonQuery();
        }

        var totalToDelete = Math.Max(1, ids.TotalCount);
        var deletedSoFar = 0;
        var activityDeleted = 0;
        var programDeleted = 0;

        void ReportDeletionProgress(int deletedInBatch, string tableDescription)
        {
            deletedSoFar += Math.Max(0, deletedInBatch);
            var ratio = Math.Min(1D, (double)deletedSoFar / totalToDelete);
            var percent = 10 + (int)Math.Round(ratio * 65D, MidpointRounding.AwayFromZero);
            progress.Report(new ExportProgressInfo(
                percent,
                $"{percent}%   Removing old {tableDescription} ({deletedSoFar:N0} of {ids.TotalCount:N0})"));
        }

        progress.Report(new ExportProgressInfo(8, $"8%   Removing {ids.TotalCount:N0} conflicting record(s)"));

        using (var transaction = connection.BeginTransaction())
        {
            activityDeleted += DeleteIds(
                connection, transaction, "ActivityEvents", ids.ActivityIds,
                deleted => ReportDeletionProgress(deleted, "file activity records"));

            programDeleted += DeleteIds(
                connection, transaction, "ProgramEvents", ids.ProgramIds,
                deleted => ReportDeletionProgress(deleted, "app activity records"));

            DeleteIds(
                connection, transaction, "UserEvents", ids.UserIds,
                deleted => ReportDeletionProgress(deleted, "user activity records"));

            progress.Report(new ExportProgressInfo(80, "80%  Committing database changes"));
            transaction.Commit();
        }

        progress.Report(new ExportProgressInfo(86, "86%  Compacting the DeskPulse database"));
        using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();
        }

        progress.Report(new ExportProgressInfo(98, "98%  Finalising cleaned database"));

        return new MaintenanceExclusionCleanupResult
        {
            ActivityRecordsDeleted = activityDeleted,
            ProgramRecordsDeleted = programDeleted
        };
    }

    private static int DeleteIds(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        IReadOnlyList<long> ids,
        Action<int> batchCompleted)
    {
        const int batchSize = 400;
        var deletedTotal = 0;

        for (var offset = 0; offset < ids.Count; offset += batchSize)
        {
            var batch = ids.Skip(offset).Take(batchSize).ToArray();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var names = new List<string>();
            for (var i = 0; i < batch.Length; i++)
            {
                var name = "$id" + i;
                names.Add(name);
                command.Parameters.AddWithValue(name, batch[i]);
            }
            command.CommandText = $"DELETE FROM {table} WHERE Id IN ({string.Join(",", names)});";
            var deleted = command.ExecuteNonQuery();
            deletedTotal += deleted;
            batchCompleted(deleted);
        }

        return deletedTotal;
    }

    private static bool FilePatternMatches(string fullPath, string pattern)
    {
        pattern = (pattern ?? "").Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        if (Path.IsPathRooted(pattern) && !ContainsWildcard(pattern))
        {
            try { return Path.GetFullPath(fullPath).Equals(Path.GetFullPath(pattern), StringComparison.OrdinalIgnoreCase); }
            catch { return fullPath.Equals(pattern, StringComparison.OrdinalIgnoreCase); }
        }
        return TextPatternMatches(Path.GetFileName(fullPath), pattern) || TextPatternMatches(fullPath, pattern);
    }

    private static bool FolderPatternMatches(string fullPath, string folder, string pattern, bool includeSubfolders)
    {
        var candidate = string.IsNullOrWhiteSpace(folder) ? Path.GetDirectoryName(fullPath) ?? "" : folder;
        pattern = (pattern ?? "").Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (ContainsWildcard(pattern)) return TextPatternMatches(candidate, pattern);
        try
        {
            var candidateFull = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var ruleFull = Path.GetFullPath(pattern).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return candidateFull.Equals(ruleFull, StringComparison.OrdinalIgnoreCase) ||
                   (includeSubfolders && candidateFull.StartsWith(ruleFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        catch { return candidate.Equals(pattern, StringComparison.OrdinalIgnoreCase); }
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


    private void ExportButton_Click(object? sender, EventArgs e)
    {
        var grid = GetActiveGrid();
        if (grid == null || grid.Rows.Count == 0)
        {
            MessageBox.Show(this, "There are no records on the current page to export.", "Export current view", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sectionName = tabs.SelectedTab?.Text ?? "Log";
        var safeSectionName = string.Concat(sectionName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Replace(" ", "-");
        var currentPage = GetActivePage() + 1;

        using var dialog = new SaveFileDialog
        {
            Title = "Export current log view",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = $"DeskPulse-{safeSectionName}-{dateStart.Value:yyyyMMdd}-{dateEnd.Value:yyyyMMdd}-Page-{currentPage}.xlsx"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            statusLabel.Text = "Exporting the current tab and page...";
            Application.DoEvents();

            using var workbook = new XLWorkbook();
            var worksheetName = sectionName.Length > 31 ? sectionName[..31] : sectionName;
            var worksheet = workbook.Worksheets.Add(worksheetName);

            var exportedColumns = grid.Columns
                .Cast<DataGridViewColumn>()
                .Where(column => column.Visible && column is not DataGridViewButtonColumn)
                .OrderBy(column => column.DisplayIndex)
                .ToList();

            for (var columnIndex = 0; columnIndex < exportedColumns.Count; columnIndex++)
            {
                var cell = worksheet.Cell(1, columnIndex + 1);
                cell.Value = exportedColumns[columnIndex].HeaderText;
                cell.Style.Font.Bold = true;
            }

            for (var rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
            {
                var gridRow = grid.Rows[rowIndex];
                for (var columnIndex = 0; columnIndex < exportedColumns.Count; columnIndex++)
                {
                    var value = gridRow.Cells[exportedColumns[columnIndex].Index].Value;
                    worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value?.ToString() ?? string.Empty;
                }
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.RangeUsed()?.SetAutoFilter();
            worksheet.Columns().AdjustToContents(8, 60);
            workbook.SaveAs(dialog.FileName);

            statusLabel.Text = $"Exported {grid.Rows.Count:N0} record(s) from {sectionName}, page {currentPage:N0}.";
            Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            statusLabel.Text = "The current view could not be exported.";
            MessageBox.Show(this, "DeskPulse could not export the current log view.\n\n" + ex.Message, "Export current view", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
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
        if ((page + 1) * PageSize >= total) return;
        SetActivePage(page + 1);
        RefreshActiveTab();
    }

    private void LastPageButton_Click(object? sender, EventArgs e)
    {
        var total = GetActiveTotal();
        var lastPage = Math.Max(0, (int)Math.Ceiling(total / (double)PageSize) - 1);
        if (GetActivePage() == lastPage) return;
        SetActivePage(lastPage);
        RefreshActiveTab();
    }

    private void ResetPages()
    {
        _folderPage = 0;
        _appPage = 0;
        _filePage = 0;
        _userPage = 0;
    }

    private int GetActivePage() => tabs.SelectedTab?.Text switch
    {
        "Folder Activity" => _folderPage,
        "App Activity" => _appPage,
        "User Activity" => _userPage,
        _ => _filePage
    };

    private int GetActiveTotal() => tabs.SelectedTab?.Text switch
    {
        "Folder Activity" => _folderTotal,
        "App Activity" => _appTotal,
        "User Activity" => _userTotal,
        _ => _fileTotal
    };

    private void SetActivePage(int page)
    {
        page = Math.Max(0, page);
        switch (tabs.SelectedTab?.Text)
        {
            case "Folder Activity": _folderPage = page; break;
            case "App Activity": _appPage = page; break;
            case "User Activity": _userPage = page; break;
            default: _filePage = page; break;
        }
    }

    private static int ClampPage(int page, int total)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        return Math.Clamp(page, 0, totalPages - 1);
    }

    private void ClampAllPages()
    {
        _folderPage = ClampPage(_folderPage, _folderTotal);
        _appPage = ClampPage(_appPage, _appTotal);
        _filePage = ClampPage(_filePage, _fileTotal);
        _userPage = ClampPage(_userPage, _userTotal);
    }

    private void UpdatePagingControls()
    {
        var total = GetActiveTotal();
        var page = GetActivePage();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (page >= totalPages)
        {
            page = totalPages - 1;
            SetActivePage(page);
        }

        firstPageButton.Enabled = page > 0;
        previousPageButton.Enabled = page > 0;
        nextPageButton.Enabled = (page + 1) * PageSize < total;
        lastPageButton.Enabled = (page + 1) * PageSize < total;
        exportButton.Enabled = total > 0 && GetActiveGrid()?.Rows.Count > 0;
        pageLabel.Text = $"Page {page + 1:N0} of {totalPages:N0}   ({total:N0} record(s))";
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
                case "Folder Activity":
                    _folderTotal = CountEntries("ActivityEvents", start, endExclusive);
                    _folderPage = ClampPage(_folderPage, _folderTotal);
                    PopulateFolderGrid(ReadFileEntries(start, endExclusive, _folderPage));
                    gridFolder.ClearSelection();
                    break;
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
                    _fileTotal = CountEntries("ActivityEvents", start, endExclusive);
                    _filePage = ClampPage(_filePage, _fileTotal);
                    PopulateFileGrid(ReadFileEntries(start, endExclusive, _filePage));
                    gridFile.ClearSelection();
                    break;
            }

            createRuleButton.Enabled = false;
            UpdatePagingControls();
            statusLabel.Text = $"Showing up to {PageSize:N0} records on the current page.";
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
            _fileTotal = CountEntries("ActivityEvents", start, endExclusive);
            _folderTotal = _fileTotal;
            _appTotal = CountEntries("ProgramEvents", start, endExclusive);
            _userTotal = CountEntries("UserEvents", start, endExclusive);
            ClampAllPages();

            var fileEntries = ReadFileEntries(start, endExclusive, _filePage);
            var folderEntries = ReadFileEntries(start, endExclusive, _folderPage);
            var appEntries = ReadAppEntries(start, endExclusive, _appPage);
            var userEntries = ReadUserEntries(start, endExclusive, _userPage);

            PopulateFileGrid(fileEntries);
            PopulateFolderGrid(folderEntries);
            PopulateAppGrid(appEntries);
            PopulateUserGrid(userEntries);

            gridFolder.ClearSelection();
            gridApp.ClearSelection();
            gridFile.ClearSelection();
            gridUser.ClearSelection();
            createRuleButton.Enabled = false;

            UpdatePagingControls();
            statusLabel.Text = $"Loaded page-size {PageSize:N0}. File: {_fileTotal:N0}; Folder view: {_folderTotal:N0}; App: {_appTotal:N0}; User: {_userTotal:N0}.";
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

    private List<LogViewEntry> ReadFileEntries(DateTime start, DateTime endExclusive, int page)
    {
        const string sql = """
            SELECT Id, CreatedAt, ActivityType, FullPath, FolderPath, FileName, Extension,
                   DateOpened, TimeOpened, SizeAtOpening, FirstWriteDate, FirstWriteTime,
                   LastWriteDate, LastWriteTime, WriteCount, SizeAtLastWrite, DateClosed,
                   TimeClosed, SizeAtClosing, InferredAction, ProcessName, ProcessId, Note
            FROM ActivityEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT $limit OFFSET $offset;
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
                ["Inferred Action"] = ReadText(reader, 19), ["Process"] = ReadText(reader, 20), ["Process ID"] = ReadText(reader, 21), ["Note"] = ReadText(reader, 22)
            };

            return new LogViewEntry(ReadText(reader, 0), createdAt, EventDate(createdAt, ReadText(reader, 7), ReadText(reader, 10), ReadText(reader, 12), ReadText(reader, 16)),
                EventTime(createdAt, ReadText(reader, 8), ReadText(reader, 11), ReadText(reader, 13), ReadText(reader, 17)),
                ReadText(reader, 19), file, folder, ReadText(reader, 20), ReadText(reader, 21), fullPath, fields);
        });
    }

    private List<LogViewEntry> ReadAppEntries(DateTime start, DateTime endExclusive, int page)
    {
        const string sql = """
            SELECT Id, CreatedAt, EventDate, EventTime, EventType, EventDescription, ProgramName,
                   ProcessId, FilePath, WindowTitle, UserName, MachineName, AppVersion, Note
            FROM ProgramEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT $limit OFFSET $offset;
            """;

        return ReadEntries(sql, start, endExclusive, page, reader =>
        {
            var fields = new Dictionary<string, string>
            {
                ["ID"] = ReadText(reader, 0), ["Created At"] = ReadText(reader, 1), ["Date"] = ReadText(reader, 2), ["Time"] = ReadText(reader, 3),
                ["Event Type"] = ReadText(reader, 4), ["Event"] = ReadText(reader, 5), ["App"] = ReadText(reader, 6), ["Process ID"] = ReadText(reader, 7),
                ["App Path"] = ReadText(reader, 8), ["Window Title"] = ReadText(reader, 9), ["User"] = ReadText(reader, 10),
                ["Computer"] = ReadText(reader, 11), ["DeskPulse Version"] = ReadText(reader, 12), ["Note"] = ReadText(reader, 13)
            };
            return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3), ReadText(reader, 4), ReadText(reader, 6), "", ReadText(reader, 6), ReadText(reader, 7), ReadText(reader, 8), fields);
        });
    }

    private List<LogViewEntry> ReadUserEntries(DateTime start, DateTime endExclusive, int page)
    {
        const string sql = """
            SELECT Id, CreatedAt, EventDate, EventTime, EventType, EventDescription, UserName,
                   MachineName, ProcessName, ProcessId, AppVersion, Note
            FROM UserEvents
            WHERE CreatedAt >= $start AND CreatedAt < $end
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT $limit OFFSET $offset;
            """;

        return ReadEntries(sql, start, endExclusive, page, reader =>
        {
            var fields = new Dictionary<string, string>
            {
                ["ID"] = ReadText(reader, 0), ["Created At"] = ReadText(reader, 1), ["Date"] = ReadText(reader, 2), ["Time"] = ReadText(reader, 3),
                ["Event Type"] = ReadText(reader, 4), ["Event"] = ReadText(reader, 5), ["User"] = ReadText(reader, 6), ["Computer"] = ReadText(reader, 7),
                ["Process"] = ReadText(reader, 8), ["Process ID"] = ReadText(reader, 9), ["DeskPulse Version"] = ReadText(reader, 10), ["Note"] = ReadText(reader, 11)
            };
            return new LogViewEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3), ReadText(reader, 4), ReadText(reader, 5), "", ReadText(reader, 6), ReadText(reader, 9), "", fields);
        });
    }

    private List<LogViewEntry> ReadEntries(string sql, DateTime start, DateTime endExclusive, int page, Func<SqliteDataReader, LogViewEntry> factory)
    {
        var result = new List<LogViewEntry>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endExclusive.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$limit", PageSize);
        command.Parameters.AddWithValue("$offset", Math.Max(0, page) * PageSize);
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

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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

    private static string EventTime(string createdAt, params string[] candidates)
    {
        foreach (var candidate in candidates) if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
        return createdAt.Length >= 19 ? createdAt.Substring(11, Math.Min(12, createdAt.Length - 11)) : "";
    }

    private void PopulateFileGrid(IEnumerable<LogViewEntry> entries)
    {
        gridFile.Rows.Clear();
        foreach (var e in entries) AddRow(gridFile, e, e.Id, e.Date, e.Time, e.EventType, e.Subject, e.Folder, e.App);
    }

    private void PopulateFolderGrid(IEnumerable<LogViewEntry> entries)
    {
        gridFolder.Rows.Clear();
        foreach (var e in entries) AddRow(gridFolder, e, e.Id, e.Date, e.Time, e.Folder, e.Subject, e.EventType, e.App);
    }

    private void PopulateAppGrid(IEnumerable<LogViewEntry> entries)
    {
        gridApp.Rows.Clear();
        foreach (var e in entries) AddRow(gridApp, e, e.Id, e.Date, e.Time, e.EventType, e.App, e.ProcessId, e.Path);
    }

    private void PopulateUserGrid(IEnumerable<LogViewEntry> entries)
    {
        gridUser.Rows.Clear();
        foreach (var e in entries) AddRow(gridUser, e, e.Id, e.Date, e.Time, e.EventType, e.Subject, e.App, e.Fields.TryGetValue("Computer", out var computer) ? computer : "");
    }

    private static void AddRow(DataGridView grid, LogViewEntry entry, params object[] values)
    {
        var rowIndex = grid.Rows.Add(values);
        grid.Rows[rowIndex].Tag = entry;
    }

    private static void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "More") return;
        ShowDetails(grid.Rows[e.RowIndex]);
    }

    private static void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is DataGridView grid && e.RowIndex >= 0) ShowDetails(grid.Rows[e.RowIndex]);
    }

    private static void ShowDetails(DataGridViewRow row)
    {
        if (row.Tag is not LogViewEntry entry) return;
        using var details = new LogEntryDetailsForm(entry.Fields);
        details.ShowDialog(row.DataGridView?.FindForm());
    }
}

public sealed record LogViewEntry(
    string Id,
    string CreatedAt,
    string Date,
    string Time,
    string EventType,
    string Subject,
    string Folder,
    string App,
    string ProcessId,
    string Path,
    IReadOnlyDictionary<string, string> Fields);
