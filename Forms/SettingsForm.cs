using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

namespace DeskPulse;

public partial class SettingsForm : Form
{
    private readonly Dictionary<string, CheckedListBox> _exportFieldListsBySheetId = new(StringComparer.OrdinalIgnoreCase);
    private DataGridView _loggingRulesGridView = null!;
    private TextBox _manualLoggingRuleTextBox = null!;
    private RadioButton _manualRuleFolderRadioButton = null!;
    private RadioButton _manualRuleFileRadioButton = null!;
    private RadioButton _manualRuleProcessRadioButton = null!;
    private RadioButton _manualRuleExcludeRadioButton = null!;
    private RadioButton _manualRuleIncludeRadioButton = null!;
    private CheckBox _manualRuleSubfoldersCheckBox = null!;
    private DataGridView _statisticsGrid = null!;
    private ComboBox _statisticsViewComboBox = null!;
    private readonly bool _maintenanceOnly;
    private bool _updatingExportUi;

    public SettingsForm(bool maintenanceOnly = false)
    {
        _maintenanceOnly = maintenanceOnly;

        InitializeComponent();

        Text = maintenanceOnly ? "DeskPulse Maintenance" : "DeskPulse Settings";

        var settings = AppSettings.Load();

        if (maintenanceOnly)
        {
            _settingsTabControl.TabPages.Clear();
            _settingsTabControl.TabPages.Add(_maintenanceTabPage);
            LoadDesignerMaintenance(settings);
            _settingsTabControl.SelectedTab = _maintenanceTabPage;
        }
        else
        {
            _settingsTabControl.TabPages.Clear();
            _settingsTabControl.TabPages.Add(_generalTabPage);
            _settingsTabControl.TabPages.Add(_filesTabPage);
            _settingsTabControl.TabPages.Add(_exportOptionsTabPage);

            LoadDesignerSettings(settings);
            LoadDesignerMaintenance(settings);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        SaveSettings();
    }

    private TabPage CreateTabPage(string text)
    {
        return new TabPage
        {
            Text = text,
            BackColor = System.Drawing.SystemColors.Window,
            Padding = new Padding(16)
        };
    }

    private GroupBox CreateGroupBox(string text, int left, int top, int width, int height)
    {
        return new GroupBox
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Padding = new Padding(12),
            BackColor = System.Drawing.SystemColors.Window
        };
    }

    private static Label CreateHintLabel(string text, int left, int top, int width, int height)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            ForeColor = System.Drawing.SystemColors.GrayText
        };
    }

    private Button CreateActionButton(string text, int left, int top, int width)
    {
        return new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.System
        };
    }

    private void SetButtonToolTip(Control control, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _buttonToolTip.SetToolTip(control, text);
    }

    private void LoadDesignerSettings(AppSettings settings)
    {
        _startWithWindowsCheckBox.Checked = settings.StartWithWindows || StartupTaskManager.IsEnabled();
        _logProgramActivityCheckBox.Checked = settings.LogProgramActivity;
        _dataFolderTextBox.Text = settings.DataFolderPath;
        _ignoreTempFoldersCheckBox.Checked = settings.IgnoreTempFolders;

        LoadExtensionLists(settings);
        LoadExportOptions(settings);
    }


    private void LoadDesignerMaintenance(AppSettings settings)
    {
        _maintenanceDatabaseInfoTextBox.Text = GetDatabaseOverviewForMaintenance(settings);
        _maintenanceDiagnosticsStatusLabel.Text = AppRuntime.DebugLoggingEnabled
            ? "Diagnostic logging is on. Normal -debug logs accepted monitored events only; add -debug-skipped only for full skip tracing."
            : "Diagnostic logging is off. Start DeskPulse with -debug to record accepted monitored ETW file events.";
        _maintenanceDiagnosticsStatusLabel.ForeColor = AppRuntime.DebugLoggingEnabled
            ? System.Drawing.SystemColors.ControlText
            : System.Drawing.SystemColors.GrayText;
        _maintenanceRegistryPathLabel.Text = "Registry settings location: " + AppSettings.GetRegistryPathForDisplay();

        PrepareDesignerStatisticsGrid();
        PrepareDesignerLoggingRulesGrid();

        if (_statisticsViewComboBox != null && _statisticsViewComboBox.SelectedIndex < 0 && _statisticsViewComboBox.Items.Count > 0)
            _statisticsViewComboBox.SelectedIndex = 0;

        LoadLoggingRulesGrid(settings.LoggingRules.Count == 0
            ? AppSettings.BuildLoggingRulesFromLegacy(settings.ExcludedFolders, settings.ExcludedProcesses)
            : settings.LoggingRules);

        RefreshMaintenanceStatistics(settings);
        RefreshDesignerToolTips();
    }

    private void PrepareDesignerStatisticsGrid()
    {
        if (_statisticsGrid == null)
            return;

        if (_statisticsGrid.Tag?.ToString() == "configured")
            return;

        var statisticsContextMenu = new ContextMenuStrip();
        statisticsContextMenu.Items.Add("Add exact file exclusion", null, (_, _) => AddSelectedStatisticValueToExclusion("file"));
        statisticsContextMenu.Items.Add("Add folder exclusion", null, (_, _) => AddSelectedStatisticValueToExclusion("folder"));
        statisticsContextMenu.Items.Add("Add process exclusion", null, (_, _) => AddSelectedStatisticValueToExclusion("process"));
        _statisticsGrid.ContextMenuStrip = statisticsContextMenu;
        _statisticsGrid.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;

            var hit = _statisticsGrid.HitTest(e.X, e.Y);

            if (hit.RowIndex < 0)
                return;

            if (!_statisticsGrid.Rows[hit.RowIndex].Selected)
            {
                _statisticsGrid.ClearSelection();
                _statisticsGrid.Rows[hit.RowIndex].Selected = true;
            }

            _statisticsGrid.CurrentCell = _statisticsGrid.Rows[hit.RowIndex].Cells[Math.Max(0, hit.ColumnIndex)];
        };
        _statisticsGrid.Tag = "configured";
    }

    private void PrepareDesignerLoggingRulesGrid()
    {
        if (_loggingRulesGridView == null)
            return;

        if (_loggingRulesGridView.Columns.Count == 0)
        {
            _loggingRulesGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", Width = 34 });
            _loggingRulesGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Exclude", HeaderText = "Exclude", Width = 58 });
            _loggingRulesGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Include", Width = 58 });
            _loggingRulesGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleType", HeaderText = "Type", Width = 68, ReadOnly = true });
            _loggingRulesGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Subfolders", HeaderText = "Sub", Width = 42 });
            _loggingRulesGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Rule / path / process", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 480 });
        }

        if (_loggingRulesGridView.Tag?.ToString() == "configured")
            return;

        _loggingRulesGridView.ColumnHeadersDefaultCellStyle.Font = System.Drawing.SystemFonts.MessageBoxFont;
        _loggingRulesGridView.DefaultCellStyle.Font = System.Drawing.SystemFonts.MessageBoxFont;
        _loggingRulesGridView.RowTemplate.Height = 22;
        _loggingRulesGridView.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                _loggingRulesGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _loggingRulesGridView.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = _loggingRulesGridView.Rows[e.RowIndex];
            var columnName = _loggingRulesGridView.Columns[e.ColumnIndex].Name;

            if (columnName.Equals("Exclude", StringComparison.OrdinalIgnoreCase) && IsCellChecked(row.Cells["Exclude"]))
                row.Cells["Include"].Value = false;

            if (columnName.Equals("Include", StringComparison.OrdinalIgnoreCase) && IsCellChecked(row.Cells["Include"]))
                row.Cells["Exclude"].Value = false;

            if (!IsCellChecked(row.Cells["Exclude"]) && !IsCellChecked(row.Cells["Include"]))
                row.Cells["Exclude"].Value = true;

            var changedRuleType = row.Cells["RuleType"].Value?.ToString() ?? "";
            if (changedRuleType.Equals("Process", StringComparison.OrdinalIgnoreCase) || changedRuleType.Equals("File", StringComparison.OrdinalIgnoreCase))
                row.Cells["Subfolders"].Value = false;
        };
        _loggingRulesGridView.Tag = "configured";
    }

    private void RefreshDesignerToolTips()
    {
        SetButtonToolTip(_maintenanceRefreshDatabaseButton, "Refreshes the database size and record-count display. Does not delete or change data.");
        SetButtonToolTip(_maintenanceOpenDataFolderButton, "Opens the DeskPulse data folder in File Explorer. Does not delete or change data.");
        SetButtonToolTip(_maintenanceOpenProgramFolderButton, "Opens the folder where DeskPulse is running from. Does not delete or change data.");
        SetButtonToolTip(_maintenanceStatisticsRefreshButton, "Reloads the selected Top 100 statistics view from the database. Does not delete or change data.");
        SetButtonToolTip(_maintenanceAddFileExclusionButton, "Adds the selected Top 100 full-path item as an exact file Exclude rule. Affects future logging only until past-record cleanup is run.");
        SetButtonToolTip(_maintenanceAddFolderExclusionButton, "Adds the selected Top 100 value as a folder Exclude rule. Affects future logging only until past-record cleanup is run.");
        SetButtonToolTip(_maintenanceAddProcessExclusionButton, "Adds the selected process/program value as an Exclude rule. Affects future logging only until past-record cleanup is run.");
        SetButtonToolTip(_maintenanceDeleteExportButton, "Deletes only the generated Excel export file. Does not delete the SQLite database, records, settings, or logging rules.");
        SetButtonToolTip(_maintenanceDeleteDiagnosticsButton, "Deletes only the DeskPulse diagnostic log. Does not delete database records, settings, or logging rules.");
        SetButtonToolTip(_maintenanceDeleteStartupLogButton, "Deletes only the temporary DeskPulse startup fallback log. Does not delete database records, settings, or logging rules.");
        SetButtonToolTip(_maintenanceRemoveUnwantedDataButton, "Deletes only past file/program records that match current Exclude rules in Logging Rules. Does not delete Include exceptions, the rules themselves, user/session records, settings, or the database file.");
        SetButtonToolTip(_maintenanceDeleteFileActivityButton, "Deletes ALL file open/write/close records from ActivityEvents. Keeps user/session records, program records, settings, logging rules, and the database file.");
        SetButtonToolTip(_maintenanceDeleteUserActivityButton, "Deletes ALL user/session records such as DeskPulse start/stop, PC lock/unlock, logon/logoff, and session connect/disconnect events. Keeps file/program records, settings, and logging rules.");
        SetButtonToolTip(_maintenanceDeleteProgramActivityButton, "Deletes ALL program start/close records from ProgramEvents. Keeps file activity, user/session records, settings, and logging rules.");
        SetButtonToolTip(_maintenanceDeleteAllActivityButton, "Deletes EVERYTHING recorded in DeskPulse activity tables: file activity, user/session activity, and program activity. Keeps settings, logging rules, export options, and database structure.");
        SetButtonToolTip(_maintenanceBrowseRuleButton, "Browses for the folder, exact file, or program executable used for a new logging rule. Does not add or delete anything by itself.");
        SetButtonToolTip(_maintenanceAddRuleButton, "Adds the newly entered Include or Exclude rule to the rules list. It affects future logging after Save; it does not delete past records.");
        SetButtonToolTip(_maintenanceMoveRuleUpButton, "Moves the selected logging rule up. Higher rules override lower rules. Does not delete data.");
        SetButtonToolTip(_maintenanceMoveRuleDownButton, "Moves the selected logging rule down. Higher rules override lower rules. Does not delete data.");
        SetButtonToolTip(_maintenanceRemoveRuleButton, "Removes only the selected logging rule from the list. It does not delete past database records.");
        SetButtonToolTip(_maintenanceDuplicateRuleButton, "Copies the selected logging rule so it can be edited or reordered. Does not delete data.");
        SetButtonToolTip(_maintenanceResetRulesButton, "Replaces the current rules shown in this form with the default logging rules. It does not delete past records; Save is still required.");
        SetButtonToolTip(_maintenanceRemovePastRecordsButton, "PERMANENTLY deletes past file/program records that match current Exclude rules. Does not delete the rules themselves or user/session records.");
        SetButtonToolTip(_maintenanceOpenDiagnosticsLogButton, "Opens the diagnostic log file if it exists. Does not delete or change data.");
        SetButtonToolTip(_maintenanceShowActiveExtensionsButton, "Shows the file extensions currently monitored by DeskPulse. Does not delete or change data.");
        SetButtonToolTip(_maintenanceShowStartupStatusButton, "Shows whether the Windows Task Scheduler startup entry exists and appears valid. Does not delete or change data.");
        SetButtonToolTip(_maintenanceRemoveRegistrySettingsButton, "Deletes current-user DeskPulse registry settings, including preferences and logging rules. Does not delete the database, export, diagnostic log, or program files.");
    }

    private void MaintenanceRefreshDatabaseButton_Click(object? sender, EventArgs e) => _maintenanceDatabaseInfoTextBox.Text = GetDatabaseOverviewForMaintenance(AppSettings.Load());
    private void MaintenanceOpenDataFolderButton_Click(object? sender, EventArgs e) => OpenFolder(AppSettings.Load().DataFolderPath, "DeskPulse data folder");
    private void MaintenanceOpenProgramFolderButton_Click(object? sender, EventArgs e) => OpenFolder(AppContext.BaseDirectory, "DeskPulse program folder");
    private void MaintenanceStatisticsViewComboBox_SelectedIndexChanged(object? sender, EventArgs e) => RefreshMaintenanceStatistics(AppSettings.Load());
    private void MaintenanceStatisticsRefreshButton_Click(object? sender, EventArgs e) => RefreshMaintenanceStatistics(AppSettings.Load());
    private void MaintenanceAddFileExclusionButton_Click(object? sender, EventArgs e) => AddSelectedStatisticValueToExclusion("file");
    private void MaintenanceAddFolderExclusionButton_Click(object? sender, EventArgs e) => AddSelectedStatisticValueToExclusion("folder");
    private void MaintenanceAddProcessExclusionButton_Click(object? sender, EventArgs e) => AddSelectedStatisticValueToExclusion("process");
    private void MaintenanceDeleteExportButton_Click(object? sender, EventArgs e) => DeleteGeneratedFile(AppSettings.Load().ExcelExportFilePath, "Excel export file");
    private void MaintenanceDeleteDiagnosticsButton_Click(object? sender, EventArgs e) => DeleteGeneratedFile(DiagnosticLogger.GetDiagnosticLogFilePath(), "diagnostic log");
    private void MaintenanceDeleteStartupLogButton_Click(object? sender, EventArgs e) => DeleteGeneratedFile(Path.Combine(Path.GetTempPath(), "DeskPulse-startup.log"), "startup fallback log");
    private void MaintenanceRemoveUnwantedDataButton_Click(object? sender, EventArgs e) => RemoveExcludedPastRecords(AppSettings.Load());
    private void MaintenanceDeleteFileActivityButton_Click(object? sender, EventArgs e) => ClearDatabaseTable(AppSettings.Load(), "ActivityEvents", "all file open/write/close activity records");
    private void MaintenanceDeleteUserActivityButton_Click(object? sender, EventArgs e) => ClearDatabaseTable(AppSettings.Load(), "UserEvents", "all user/session records");
    private void MaintenanceDeleteProgramActivityButton_Click(object? sender, EventArgs e) => ClearDatabaseTable(AppSettings.Load(), "ProgramEvents", "all program start/close activity records");
    private void MaintenanceDeleteAllActivityButton_Click(object? sender, EventArgs e) => ClearAllDatabaseTables(AppSettings.Load());
    private void MaintenanceBrowseRuleButton_Click(object? sender, EventArgs e) => BrowseForLoggingRulePath();
    private void MaintenanceAddRuleButton_Click(object? sender, EventArgs e) => AddManualLoggingRuleEntry();
    private void ManualRuleTypeRadioButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (_manualRuleSubfoldersCheckBox != null)
            _manualRuleSubfoldersCheckBox.Enabled = _manualRuleFolderRadioButton?.Checked == true;
    }
    private void MaintenanceMoveRuleUpButton_Click(object? sender, EventArgs e) => MoveSelectedExclusionRuleRow(_loggingRulesGridView, -1);
    private void MaintenanceMoveRuleDownButton_Click(object? sender, EventArgs e) => MoveSelectedExclusionRuleRow(_loggingRulesGridView, 1);
    private void MaintenanceRemoveRuleButton_Click(object? sender, EventArgs e) => RemoveSelectedLoggingRuleRow();
    private void MaintenanceDuplicateRuleButton_Click(object? sender, EventArgs e) => DuplicateSelectedLoggingRuleRow();
    private void MaintenanceResetRulesButton_Click(object? sender, EventArgs e) => LoadLoggingRulesGrid(AppSettings.GetDefaultLoggingRules());
    private void MaintenanceRemovePastRecordsButton_Click(object? sender, EventArgs e) => RemoveExcludedPastRecords(AppSettings.Load());
    private void MaintenanceOpenDiagnosticsLogButton_Click(object? sender, EventArgs e) => OpenFile(DiagnosticLogger.GetDiagnosticLogFilePath(), "DeskPulse diagnostic log");
    private void MaintenanceShowActiveExtensionsButton_Click(object? sender, EventArgs e) => ShowActiveExtensions();
    private void MaintenanceShowStartupStatusButton_Click(object? sender, EventArgs e) => ShowStartupStatus();
    private void MaintenanceRemoveRegistrySettingsButton_Click(object? sender, EventArgs e) => RemoveRegistrySettings();

    private void BrowseDataFolderButton_Click(object? sender, EventArgs e)
    {
        BrowseForDataFolder();
    }

    private void AddExtensionButton_Click(object? sender, EventArgs e)
    {
        AddSelectedExtension();
    }

    private void RemoveExtensionButton_Click(object? sender, EventArgs e)
    {
        RemoveSelectedExtension();
    }

    private void AvailableExtensionsListBox_DragDrop(object? sender, DragEventArgs e)
    {
        RemoveDraggedExtension(e);
    }

    private void MonitoredExtensionsListBox_DragDrop(object? sender, DragEventArgs e)
    {
        AddDraggedExtension(e);
    }

    private void ExportSheetsCheckedListBox_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_updatingExportUi)
            return;

        if (IsHandleCreated)
            BeginInvoke(new Action(RebuildExportFieldTabs));
        else
            RebuildExportFieldTabs();
    }

    private void MoveSheetUpButton_Click(object? sender, EventArgs e)
    {
        MoveSelectedExportSheet(-1);
    }

    private void MoveSheetDownButton_Click(object? sender, EventArgs e)
    {
        MoveSelectedExportSheet(1);
    }

    private void ResetExportSheetsButton_Click(object? sender, EventArgs e)
    {
        ResetExportSheetsToDefault();
    }

    private void MoveFieldUpButton_Click(object? sender, EventArgs e)
    {
        MoveSelectedExportField(-1);
    }

    private void MoveFieldDownButton_Click(object? sender, EventArgs e)
    {
        MoveSelectedExportField(1);
    }

    private void SelectAllFieldsButton_Click(object? sender, EventArgs e)
    {
        SetCurrentExportFieldChecks(true);
    }

    private void ClearFieldsButton_Click(object? sender, EventArgs e)
    {
        SetCurrentExportFieldChecks(false);
    }

    private void BuildGeneralTab(TabPage generalTab, AppSettings settings)
    {
        var startupGroup = CreateGroupBox("Windows startup", 24, 24, 820, 150);

        _startWithWindowsCheckBox.Left = 18;
        _startWithWindowsCheckBox.Top = 32;
        _startWithWindowsCheckBox.Width = 520;
        _startWithWindowsCheckBox.Text = "Start DeskPulse when I log in to Windows";
        _startWithWindowsCheckBox.Checked = settings.StartWithWindows || StartupTaskManager.IsEnabled();

        var startupHintLabel = CreateHintLabel(
            "Creates a current-user Windows Task Scheduler entry and starts DeskPulse with highest privileges when you log in.",
            40,
            62,
            740,
            22);

        var quietStartupLabel = CreateHintLabel(
            "DeskPulse starts quietly in the tray. Startup errors are still shown if monitoring cannot start.",
            40,
            88,
            740,
            22);

        startupGroup.Controls.AddRange(new Control[]
        {
            _startWithWindowsCheckBox,
            startupHintLabel,
            quietStartupLabel
        });

        var behaviourGroup = CreateGroupBox("Application behaviour", 24, 196, 820, 150);

        _logProgramActivityCheckBox.Left = 18;
        _logProgramActivityCheckBox.Top = 32;
        _logProgramActivityCheckBox.Width = 520;
        _logProgramActivityCheckBox.Text = "Log program start and close activity";
        _logProgramActivityCheckBox.Checked = settings.LogProgramActivity;

        var programActivityHintLabel = CreateHintLabel(
            "Records programs that start and close in the current interactive Windows session. This is not a full system-service audit log.",
            40,
            62,
            740,
            36);

        var behaviourLabel = CreateHintLabel(
            "DeskPulse keeps running from the tray icon. Left-click opens Export/Settings; right-click opens About/Exit.",
            18,
            106,
            760,
            22);

        behaviourGroup.Controls.AddRange(new Control[]
        {
            _logProgramActivityCheckBox,
            programActivityHintLabel,
            behaviourLabel
        });

        generalTab.Controls.AddRange(new Control[]
        {
            startupGroup,
            behaviourGroup
        });
    }


    private void BuildFilesTab(TabPage filesTab, AppSettings settings)
    {
        var storageGroup = CreateGroupBox("Storage", 24, 24, 820, 122);

        var dataFolderLabel = new Label
        {
            Text = "Data folder",
            Left = 18,
            Top = 34,
            Width = 90
        };

        _dataFolderTextBox.Left = 112;
        _dataFolderTextBox.Top = 30;
        _dataFolderTextBox.Width = 560;
        _dataFolderTextBox.Text = settings.DataFolderPath;

        var browseButton = CreateActionButton("Browse...", 686, 28, 100);
        browseButton.Click += (_, _) => BrowseForDataFolder();

        var databaseLabel = CreateHintLabel(
            "Live data: DeskPulse.db    Export report: DeskPulse-export.xlsx",
            112,
            62,
            650,
            22);

        storageGroup.Controls.AddRange(new Control[]
        {
            dataFolderLabel,
            _dataFolderTextBox,
            browseButton,
            databaseLabel
        });

        var filterGroup = CreateGroupBox("File activity filters", 24, 166, 820, 88);

        _ignoreTempFoldersCheckBox.Left = 18;
        _ignoreTempFoldersCheckBox.Top = 30;
        _ignoreTempFoldersCheckBox.Width = 360;
        _ignoreTempFoldersCheckBox.Text = "Ignore temporary-folder activity";
        _ignoreTempFoldersCheckBox.Checked = settings.IgnoreTempFolders;

        var tempHintLabel = CreateHintLabel(
            "Recommended. Turn off only when you deliberately want to include Windows temp-folder activity.",
            40,
            56,
            720,
            22);

        filterGroup.Controls.AddRange(new Control[]
        {
            _ignoreTempFoldersCheckBox,
            tempHintLabel
        });

        var fileTypesGroup = CreateGroupBox("Monitored file types", 24, 274, 820, 280);

        var availableLabel = new Label
        {
            Text = "Available file types",
            Left = 18,
            Top = 28,
            Width = 300
        };

        _availableExtensionsListBox.Left = 18;
        _availableExtensionsListBox.Top = 52;
        _availableExtensionsListBox.Width = 330;
        _availableExtensionsListBox.Height = 190;
        _availableExtensionsListBox.DisplayMember = nameof(RegisteredFileTypeInfo.DisplayName);
        _availableExtensionsListBox.SelectionMode = SelectionMode.One;
        _availableExtensionsListBox.IntegralHeight = false;
        _availableExtensionsListBox.MouseDown += ListBoxMouseDown;
        _availableExtensionsListBox.AllowDrop = true;
        _availableExtensionsListBox.DragEnter += ListBoxDragEnter;
        _availableExtensionsListBox.DragDrop += (_, e) => RemoveDraggedExtension(e);

        var monitoredLabel = new Label
        {
            Text = "Currently monitored",
            Left = 472,
            Top = 28,
            Width = 300
        };

        _monitoredExtensionsListBox.Left = 472;
        _monitoredExtensionsListBox.Top = 52;
        _monitoredExtensionsListBox.Width = 330;
        _monitoredExtensionsListBox.Height = 190;
        _monitoredExtensionsListBox.SelectionMode = SelectionMode.One;
        _monitoredExtensionsListBox.IntegralHeight = false;
        _monitoredExtensionsListBox.MouseDown += ListBoxMouseDown;
        _monitoredExtensionsListBox.AllowDrop = true;
        _monitoredExtensionsListBox.DragEnter += ListBoxDragEnter;
        _monitoredExtensionsListBox.DragDrop += (_, e) => AddDraggedExtension(e);

        var addButton = CreateActionButton("Add  >", 366, 95, 88);
        addButton.Click += (_, _) => AddSelectedExtension();

        var removeButton = CreateActionButton("<  Remove", 366, 135, 88);
        removeButton.Click += (_, _) => RemoveSelectedExtension();

        var dragHintLabel = CreateHintLabel(
            "You can also drag file types between the lists.",
            18,
            246,
            760,
            22);

        fileTypesGroup.Controls.AddRange(new Control[]
        {
            availableLabel,
            _availableExtensionsListBox,
            addButton,
            removeButton,
            monitoredLabel,
            _monitoredExtensionsListBox,
            dragHintLabel
        });

        filesTab.Controls.AddRange(new Control[]
        {
            storageGroup,
            filterGroup,
            fileTypesGroup
        });
    }


    private void BuildExportOptionsTab(TabPage exportOptionsTab, AppSettings settings)
    {
        var introLabel = CreateHintLabel(
            "Choose which Excel worksheets DeskPulse creates, then choose the columns and column order for each selected worksheet.",
            24,
            22,
            820,
            28);

        var worksheetGroup = CreateGroupBox("Worksheets", 24, 62, 300, 492);

        _exportSheetsCheckedListBox.Left = 18;
        _exportSheetsCheckedListBox.Top = 30;
        _exportSheetsCheckedListBox.Width = 262;
        _exportSheetsCheckedListBox.Height = 350;
        _exportSheetsCheckedListBox.DisplayMember = nameof(ExportSheetOption.DisplayName);
        _exportSheetsCheckedListBox.CheckOnClick = true;
        _exportSheetsCheckedListBox.SelectionMode = SelectionMode.One;
        _exportSheetsCheckedListBox.IntegralHeight = false;
        _exportSheetsCheckedListBox.ItemCheck += (_, _) =>
        {
            if (_updatingExportUi)
                return;

            if (IsHandleCreated)
                BeginInvoke(new Action(RebuildExportFieldTabs));
            else
                RebuildExportFieldTabs();
        };

        var sheetUpButton = CreateActionButton("Move Up", 18, 394, 82);
        sheetUpButton.Click += (_, _) => MoveSelectedExportSheet(-1);

        var sheetDownButton = CreateActionButton("Move Down", 108, 394, 92);
        sheetDownButton.Click += (_, _) => MoveSelectedExportSheet(1);

        var resetButton = CreateActionButton("Reset", 208, 394, 72);
        resetButton.Click += (_, _) => ResetExportSheetsToDefault();

        var worksheetHintLabel = CreateHintLabel(
            "Ticked items become workbook tabs. Their order here becomes the Excel worksheet order.",
            18,
            430,
            260,
            44);

        worksheetGroup.Controls.AddRange(new Control[]
        {
            _exportSheetsCheckedListBox,
            sheetUpButton,
            sheetDownButton,
            resetButton,
            worksheetHintLabel
        });

        var fieldsGroup = CreateGroupBox("Worksheet columns", 344, 62, 500, 492);

        _exportFieldTabControl.Left = 18;
        _exportFieldTabControl.Top = 30;
        _exportFieldTabControl.Width = 462;
        _exportFieldTabControl.Height = 350;

        var fieldUpButton = CreateActionButton("Move Up", 18, 394, 82);
        fieldUpButton.Click += (_, _) => MoveSelectedExportField(-1);

        var fieldDownButton = CreateActionButton("Move Down", 108, 394, 92);
        fieldDownButton.Click += (_, _) => MoveSelectedExportField(1);

        var selectAllFieldsButton = CreateActionButton("Select All", 216, 394, 86);
        selectAllFieldsButton.Click += (_, _) => SetCurrentExportFieldChecks(true);

        var clearFieldsButton = CreateActionButton("Clear", 310, 394, 74);
        clearFieldsButton.Click += (_, _) => SetCurrentExportFieldChecks(false);

        var hintLabel = CreateHintLabel(
            "The order shown in each field list becomes the column order in that worksheet.",
            18,
            430,
            450,
            44);

        fieldsGroup.Controls.AddRange(new Control[]
        {
            _exportFieldTabControl,
            fieldUpButton,
            fieldDownButton,
            selectAllFieldsButton,
            clearFieldsButton,
            hintLabel
        });

        exportOptionsTab.Controls.AddRange(new Control[]
        {
            introLabel,
            worksheetGroup,
            fieldsGroup
        });

        LoadExportOptions(settings);
    }


    private static string GetDatabaseOverviewForMaintenance(AppSettings settings)
    {
        try
        {
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath);
            database.Initialize();
            var overview = database.GetMaintenanceOverview();

            return
                "Database path:" + Environment.NewLine +
                settings.DatabaseFilePath + Environment.NewLine + Environment.NewLine +
                "Database size: " + FormatBytes(overview.DatabaseBytes) + Environment.NewLine +
                "WAL size: " + FormatBytes(overview.WalBytes) + Environment.NewLine +
                "SHM size: " + FormatBytes(overview.ShmBytes) + Environment.NewLine +
                "Total database-related files: " + FormatBytes(overview.TotalDatabaseRelatedBytes) + Environment.NewLine + Environment.NewLine +
                "File activity records: " + overview.ActivityEventCount.ToString("N0", CultureInfo.InvariantCulture) + Environment.NewLine +
                "User/session records: " + overview.UserEventCount.ToString("N0", CultureInfo.InvariantCulture) + Environment.NewLine +
                "Program activity records: " + overview.ProgramEventCount.ToString("N0", CultureInfo.InvariantCulture) + Environment.NewLine +
                "Total records: " + overview.TotalRecordCount.ToString("N0", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return "Database overview could not be loaded." + Environment.NewLine + Environment.NewLine + ex.Message;
        }
    }

    private void RefreshMaintenanceStatistics(AppSettings settings)
    {
        if (_statisticsGrid == null || _statisticsViewComboBox == null)
            return;

        try
        {
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath);
            database.Initialize();

            var view = _statisticsViewComboBox.SelectedItem?.ToString() ?? "Top 100 full paths";
            var rows = database.GetTopMaintenanceStatistics(view);

            _statisticsGrid.Columns.Clear();
            _statisticsGrid.Rows.Clear();

            _statisticsGrid.Columns.Add("Rank", "Rank");
            _statisticsGrid.Columns.Add("Count", "Count");
            _statisticsGrid.Columns.Add("Value", "Value");
            _statisticsGrid.Columns.Add("Extra", "Extra");
            _statisticsGrid.Columns.Add("FirstSeen", "First Seen");
            _statisticsGrid.Columns.Add("LastSeen", "Last Seen");

            foreach (var row in rows)
            {
                _statisticsGrid.Rows.Add(
                    row.Rank,
                    row.Count.ToString("N0", CultureInfo.InvariantCulture),
                    row.Value,
                    row.Extra,
                    row.FirstSeen,
                    row.LastSeen);
            }

            if (_statisticsGrid.Columns.Count >= 6)
            {
                _statisticsGrid.Columns[0].Width = 50;
                _statisticsGrid.Columns[1].Width = 80;
                _statisticsGrid.Columns[2].FillWeight = 240;
                _statisticsGrid.Columns[3].FillWeight = 120;
                _statisticsGrid.Columns[4].Width = 110;
                _statisticsGrid.Columns[5].Width = 110;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Maintenance statistics could not be loaded.\n\n" + ex.Message,
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void AddSelectedStatisticValueToExclusion(string exclusionType)
    {
        if (_statisticsGrid == null)
            return;

        var selectedRows = _statisticsGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow && row.Cells.Count >= 4)
            .OrderBy(row => row.Index)
            .ToList();

        if (selectedRows.Count == 0 && _statisticsGrid.CurrentRow != null && _statisticsGrid.CurrentRow.Cells.Count >= 4)
            selectedRows.Add(_statisticsGrid.CurrentRow);

        if (selectedRows.Count == 0)
            return;

        if (_loggingRulesGridView == null)
        {
            MessageBox.Show(
                "Open the Logging Rules tab first, then add the selected statistics items.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var view = _statisticsViewComboBox?.SelectedItem?.ToString() ?? "";
        var addedCount = 0;
        var skippedCount = 0;
        var invalidViewShown = false;

        foreach (var row in selectedRows)
        {
            var valueColumn = row.Cells[2].Value?.ToString() ?? "";
            var extraColumn = row.Cells[3].Value?.ToString() ?? "";
            var value = valueColumn;

            if (exclusionType.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                if (!view.Equals("Top 100 full paths", StringComparison.OrdinalIgnoreCase))
                {
                    if (!invalidViewShown)
                    {
                        MessageBox.Show(
                            "Exact file exclusions can only be added from the Top 100 full paths view.",
                            "DeskPulse Maintenance",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        invalidViewShown = true;
                    }

                    skippedCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("(", StringComparison.Ordinal))
                {
                    skippedCount++;
                    continue;
                }

                AddLoggingRuleToGrid("file", value, false, false);
                addedCount++;
                continue;
            }

            if (exclusionType.Equals("folder", StringComparison.OrdinalIgnoreCase))
            {
                if (view.Equals("Top 100 full paths", StringComparison.OrdinalIgnoreCase))
                    value = PathUtilities.GetFolderPath(valueColumn).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("(", StringComparison.Ordinal))
                {
                    skippedCount++;
                    continue;
                }

                AddLoggingRuleToGrid("folder", value, false, true);
                addedCount++;
                continue;
            }

            if (exclusionType.Equals("process", StringComparison.OrdinalIgnoreCase))
            {
                if (view.Equals("Top 100 full paths", StringComparison.OrdinalIgnoreCase) ||
                    view.Equals("Top 100 extensions", StringComparison.OrdinalIgnoreCase))
                {
                    value = extraColumn;
                }

                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("(", StringComparison.Ordinal))
                {
                    skippedCount++;
                    continue;
                }

                AddLoggingRuleToGrid("process", value, false, false);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            MessageBox.Show(
                "No valid rules were added from the selected statistics rows.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var itemText = addedCount == 1 ? "rule" : "rules";
        var skippedText = skippedCount > 0
            ? Environment.NewLine + Environment.NewLine + "Skipped rows: " + skippedCount.ToString("N0", CultureInfo.InvariantCulture)
            : "";

        MessageBox.Show(
            "Added " + addedCount.ToString("N0", CultureInfo.InvariantCulture) + " Exclude " + itemText +
            " in this Maintenance window. Move them up/down if needed, then click Save to store the change." + skippedText,
            "DeskPulse Maintenance",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private DataGridView CreateLoggingRulesGrid(int left, int top, int width, int height)
    {
        var grid = new DataGridView
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = true,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            BackgroundColor = System.Drawing.SystemColors.Window,
            EditMode = DataGridViewEditMode.EditOnEnter,
            Font = System.Drawing.SystemFonts.MessageBoxFont,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            ColumnHeadersDefaultCellStyle = { Font = System.Drawing.SystemFonts.MessageBoxFont },
            DefaultCellStyle = { Font = System.Drawing.SystemFonts.MessageBoxFont },
            RowTemplate = { Height = 22 }
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled",
            HeaderText = "On",
            Width = 34
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Exclude",
            HeaderText = "Exclude",
            Width = 58
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Include",
            HeaderText = "Include",
            Width = 58
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RuleType",
            HeaderText = "Type",
            Width = 68,
            ReadOnly = true
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Subfolders",
            HeaderText = "Sub",
            Width = 42
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value",
            HeaderText = "Rule / path / process",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 480
        });

        grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = grid.Rows[e.RowIndex];
            var columnName = grid.Columns[e.ColumnIndex].Name;

            if (columnName.Equals("Exclude", StringComparison.OrdinalIgnoreCase) && IsCellChecked(row.Cells["Exclude"]))
                row.Cells["Include"].Value = false;

            if (columnName.Equals("Include", StringComparison.OrdinalIgnoreCase) && IsCellChecked(row.Cells["Include"]))
                row.Cells["Exclude"].Value = false;

            if (!IsCellChecked(row.Cells["Exclude"]) && !IsCellChecked(row.Cells["Include"]))
                row.Cells["Exclude"].Value = true;

            var changedRuleType = row.Cells["RuleType"].Value?.ToString() ?? "";
            if (changedRuleType.Equals("Process", StringComparison.OrdinalIgnoreCase) || changedRuleType.Equals("File", StringComparison.OrdinalIgnoreCase))
                row.Cells["Subfolders"].Value = false;
        };

        return grid;
    }

    private static bool IsCellChecked(DataGridViewCell cell)
    {
        try
        {
            return cell.Value is bool value && value;
        }
        catch
        {
            return false;
        }
    }

    private void LoadLoggingRulesGrid(IEnumerable<string> rules)
    {
        if (_loggingRulesGridView == null)
            return;

        _loggingRulesGridView.Rows.Clear();

        foreach (var ruleText in rules ?? Array.Empty<string>())
        {
            var rule = ExclusionRuleParser.ParseRule(ruleText);

            if (string.IsNullOrWhiteSpace(rule.Value))
                continue;

            var typeText = rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase)
                ? "Process"
                : rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase)
                    ? "File"
                    : "Folder";
            _loggingRulesGridView.Rows.Add(true, rule.IsExclude, rule.IsInclude, typeText, rule.RuleType.Equals("folder", StringComparison.OrdinalIgnoreCase) && rule.IncludeSubfolders, rule.Value);
        }
    }

    private List<string> GetLoggingRulesFromGrid()
    {
        var result = new List<string>();

        if (_loggingRulesGridView == null)
            return result;

        foreach (DataGridViewRow row in _loggingRulesGridView.Rows)
        {
            if (row.IsNewRow)
                continue;

            if (!IsCellChecked(row.Cells["Enabled"]))
                continue;

            var value = row.Cells["Value"].Value?.ToString()?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(value))
                continue;

            var ruleTypeText = row.Cells["RuleType"].Value?.ToString() ?? "Folder";
            var ruleType = ruleTypeText.Equals("Process", StringComparison.OrdinalIgnoreCase)
                ? "process"
                : ruleTypeText.Equals("File", StringComparison.OrdinalIgnoreCase)
                    ? "file"
                    : "folder";
            var isInclude = IsCellChecked(row.Cells["Include"]);
            var includeSubfolders = ruleType.Equals("folder", StringComparison.OrdinalIgnoreCase) && IsCellChecked(row.Cells["Subfolders"]);

            var rule = ExclusionRuleParser.BuildRule(ruleType, value, isInclude, includeSubfolders);

            if (!string.IsNullOrWhiteSpace(rule))
                result.Add(rule);
        }

        return result;
    }

    private void AddLoggingRuleToGrid(string ruleType, string value, bool isInclude, bool includeSubfolders)
    {
        if (_loggingRulesGridView == null)
            return;

        value = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(value))
            return;

        var typeText = ruleType.Equals("process", StringComparison.OrdinalIgnoreCase)
            ? "Process"
            : ruleType.Equals("file", StringComparison.OrdinalIgnoreCase)
                ? "File"
                : "Folder";
        var subfolders = typeText.Equals("Folder", StringComparison.OrdinalIgnoreCase) && includeSubfolders;

        foreach (DataGridViewRow row in _loggingRulesGridView.Rows)
        {
            if (row.IsNewRow)
                continue;

            var existingType = row.Cells["RuleType"].Value?.ToString() ?? "";
            var existingValue = row.Cells["Value"].Value?.ToString() ?? "";
            var existingInclude = IsCellChecked(row.Cells["Include"]);
            var existingSubfolders = IsCellChecked(row.Cells["Subfolders"]);

            if (existingType.Equals(typeText, StringComparison.OrdinalIgnoreCase) &&
                existingValue.Equals(value, StringComparison.OrdinalIgnoreCase) &&
                existingInclude == isInclude &&
                existingSubfolders == subfolders)
            {
                return;
            }
        }

        _loggingRulesGridView.Rows.Add(true, !isInclude, isInclude, typeText, subfolders, value);
    }

    private void AddManualLoggingRuleEntry()
    {
        if (_manualLoggingRuleTextBox == null)
            return;

        var isFolder = _manualRuleFolderRadioButton?.Checked == true;
        var isFile = _manualRuleFileRadioButton?.Checked == true;
        var isInclude = _manualRuleIncludeRadioButton?.Checked == true;
        var includeSubfolders = isFolder && (_manualRuleSubfoldersCheckBox?.Checked != false);
        var value = isFolder
            ? NormalizeManualExcludedFolderPath(_manualLoggingRuleTextBox.Text)
            : isFile
                ? NormalizeManualFileRule(_manualLoggingRuleTextBox.Text)
                : NormalizeManualProcessRule(_manualLoggingRuleTextBox.Text);

        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(
                isFolder ? "Enter or browse for a folder path first." : isFile ? "Enter or browse for an exact file path first." : "Enter or browse for a process/program first.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        AddLoggingRuleToGrid(isFolder ? "folder" : isFile ? "file" : "process", value, isInclude, includeSubfolders);
        _manualLoggingRuleTextBox.Clear();
    }

    private void BrowseForLoggingRulePath()
    {
        if (_manualLoggingRuleTextBox == null)
            return;

        var isFolder = _manualRuleFolderRadioButton?.Checked == true;
        var isFile = _manualRuleFileRadioButton?.Checked == true;

        if (isFolder)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select folder for DeskPulse logging rule",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(_manualLoggingRuleTextBox.Text) ? _manualLoggingRuleTextBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                _manualLoggingRuleTextBox.Text = dialog.SelectedPath;

            return;
        }

        using var fileDialog = new OpenFileDialog
        {
            Title = isFile ? "Select exact file for DeskPulse logging rule" : "Select program for DeskPulse logging rule",
            Filter = isFile ? "All files (*.*)|*.*" : "Program files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (fileDialog.ShowDialog(this) == DialogResult.OK)
            _manualLoggingRuleTextBox.Text = isFile ? fileDialog.FileName : Path.GetFileName(fileDialog.FileName);
    }

    private void RemoveSelectedLoggingRuleRow()
    {
        if (_loggingRulesGridView == null || _loggingRulesGridView.CurrentRow == null || _loggingRulesGridView.CurrentRow.IsNewRow)
            return;

        _loggingRulesGridView.Rows.RemoveAt(_loggingRulesGridView.CurrentRow.Index);
    }

    private void DuplicateSelectedLoggingRuleRow()
    {
        if (_loggingRulesGridView == null || _loggingRulesGridView.CurrentRow == null || _loggingRulesGridView.CurrentRow.IsNewRow)
            return;

        var values = new object?[_loggingRulesGridView.Columns.Count];

        for (var i = 0; i < _loggingRulesGridView.Columns.Count; i++)
            values[i] = _loggingRulesGridView.CurrentRow.Cells[i].Value;

        _loggingRulesGridView.Rows.Insert(_loggingRulesGridView.CurrentRow.Index + 1, values);
    }

    private static void MoveSelectedExclusionRuleRow(DataGridView? gridView, int direction)
    {
        if (gridView == null || gridView.CurrentRow == null || gridView.CurrentRow.IsNewRow)
            return;

        var currentIndex = gridView.CurrentRow.Index;
        var newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= gridView.Rows.Count)
            return;

        if (gridView.Rows[newIndex].IsNewRow)
            return;

        var values = new object?[gridView.Columns.Count];

        for (var i = 0; i < gridView.Columns.Count; i++)
            values[i] = gridView.CurrentRow.Cells[i].Value;

        gridView.Rows.RemoveAt(currentIndex);
        gridView.Rows.Insert(newIndex, values);

        gridView.ClearSelection();
        gridView.Rows[newIndex].Selected = true;

        if (gridView.Columns.Count > 0)
            gridView.CurrentCell = gridView.Rows[newIndex].Cells[0];
    }

    private static string NormalizeManualFileRule(string value)
    {
        value = (value ?? "").Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            value = Environment.ExpandEnvironmentVariables(value);
            value = Path.GetFullPath(value);
        }
        catch
        {
            // Keep manually entered file paths even if Windows cannot currently resolve them.
        }

        return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeManualProcessRule(string value)
    {
        value = (value ?? "").Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            value = Environment.ExpandEnvironmentVariables(value);

            if (value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
                value = Path.GetFileName(value);
        }
        catch
        {
            // Keep manually entered process names even if Windows cannot resolve them.
        }

        return value.Trim();
    }

    private static List<string> ExtractFolderRules(IEnumerable<string> loggingRules)
    {
        return (loggingRules ?? Array.Empty<string>())
            .Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("folder", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildFolderRule(rule.Value, rule.IsInclude, rule.IncludeSubfolders))
            .ToList();
    }

    private static List<string> ExtractProcessRules(IEnumerable<string> loggingRules)
    {
        return (loggingRules ?? Array.Empty<string>())
            .Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildProcessRule(rule.Value, rule.IsInclude))
            .ToList();
    }

    private static List<string> ExtractFileRules(IEnumerable<string> loggingRules)
    {
        return (loggingRules ?? Array.Empty<string>())
            .Select(ExclusionRuleParser.ParseRule)
            .Where(rule => rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rule.Value))
            .Select(rule => ExclusionRuleParser.BuildFileRule(rule.Value, rule.IsInclude))
            .ToList();
    }

    private static string NormalizeManualExcludedFolderPath(string value)
    {
        value = (value ?? "").Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            value = Environment.ExpandEnvironmentVariables(value);
            value = Path.GetFullPath(value);
        }
        catch
        {
            // Keep manually entered paths even if Windows cannot currently resolve them.
        }

        return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void MoveSelectedTextLine(TextBox? textBox, int direction)
    {
        if (textBox == null)
            return;

        var lines = textBox.Lines.ToList();

        if (lines.Count < 2)
            return;

        var currentLine = textBox.GetLineFromCharIndex(textBox.SelectionStart);
        var targetLine = currentLine + direction;

        if (currentLine < 0 || currentLine >= lines.Count || targetLine < 0 || targetLine >= lines.Count)
            return;

        (lines[currentLine], lines[targetLine]) = (lines[targetLine], lines[currentLine]);
        textBox.Lines = lines.ToArray();

        var selectionStart = 0;
        for (var i = 0; i < targetLine; i++)
            selectionStart += textBox.Lines[i].Length + Environment.NewLine.Length;

        textBox.SelectionStart = Math.Max(0, selectionStart);
        textBox.SelectionLength = textBox.Lines[targetLine].Length;
        textBox.Focus();
    }


    private void RemoveExcludedPastRecords(AppSettings settings)
    {
        var loggingRules = _loggingRulesGridView == null
            ? settings.LoggingRules
            : GetLoggingRulesFromGrid();

        var excludedFolders = ExtractFolderRules(loggingRules);
        var excludedProcesses = ExtractProcessRules(loggingRules);
        var excludedFiles = ExtractFileRules(loggingRules);

        if (excludedFolders.Count == 0 && excludedProcesses.Count == 0 && excludedFiles.Count == 0)
        {
            MessageBox.Show(
                "There are no exclude rules to apply to past records.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "This will permanently delete existing DeskPulse records that match the current exclusion lists.\n\n" +
            "Deleted data cannot be recovered from DeskPulse unless you have a backup of the SQLite database.\n\n" +
            "This action can delete:\n" +
            "- file activity records whose exact file path matches an excluded file\n" +
            "- file activity records whose folder/path matches an excluded folder\n" +
            "- file activity records whose process matches an excluded process\n" +
            "- program activity records whose program/path matches an excluded process or folder\n\n" +
            "It does not delete the exclusion settings themselves, and it does not delete user/session records.\n\n" +
            "Continue?",
            "Delete Matching Past Records",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        using var progressForm = new MaintenanceProgressForm(
            "Remove Matching Past Records",
            "Removing excluded past records",
            progress =>
            {
                using var database = new DeskPulseDatabase(settings.DatabaseFilePath);
                database.Initialize();
                return database.RemoveRecordsMatchingExclusions(excludedFolders, excludedProcesses, excludedFiles, progress);
            });

        if (progressForm.ShowDialog(this) != DialogResult.OK || progressForm.Result == null)
            return;

        var result = progressForm.Result;

        MessageBox.Show(
            "Excluded past records removed.\n\n" +
            "File activity records deleted: " + result.ActivityRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
            "Program activity records deleted: " + result.ProgramRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
            "Total records deleted: " + result.TotalRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture),
            "DeskPulse Maintenance",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void ClearDatabaseTable(AppSettings settings, string tableName, string description)
    {
        var confirm = MessageBox.Show(
            "This will permanently remove " + description + " from the DeskPulse database.\n\n" +
            "The database file and table structure will be kept.\n\n" +
            "Continue?",
            "Clear DeskPulse Records",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath);
            database.Initialize();
            var deleted = database.ClearTableRecords(tableName);

            MessageBox.Show(
                deleted.ToString("N0", CultureInfo.InvariantCulture) + " " + description + " removed.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Records could not be cleared.\n\n" + ex.Message,
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void ClearAllDatabaseTables(AppSettings settings)
    {
        var confirm = MessageBox.Show(
            "This will permanently remove all DeskPulse activity records from the database.\n\n" +
            "It clears file activity, user/session activity, and program activity records.\n" +
            "The database file and table structure will be kept.\n\n" +
            "Continue?",
            "Clear All DeskPulse Records",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath);
            database.Initialize();
            var deleted = database.ClearAllRecords();

            MessageBox.Show(
                deleted.ToString("N0", CultureInfo.InvariantCulture) + " total records removed.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Records could not be cleared.\n\n" + ex.Message,
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void DeleteGeneratedFile(string filePath, string description)
    {
        var confirm = MessageBox.Show(
            "This will delete the " + description + ".\n\n" +
            "Path:\n" + filePath + "\n\n" +
            "Continue?",
            "Delete Generated File",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            MessageBox.Show(
                "The " + description + " was deleted or did not exist.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The " + description + " could not be deleted.\n\n" + ex.Message,
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void ShowStartupStatus()
    {
        var enabled = StartupTaskManager.IsEnabled();

        MessageBox.Show(
            "Task Scheduler startup task: " + (enabled ? "Enabled" : "Not detected") + Environment.NewLine +
            "Task name: DeskPulse" + Environment.NewLine +
            "Expected trigger: ONLOGON" + Environment.NewLine +
            "Expected run level: highest available privileges",
            "DeskPulse Startup Status",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes";

        var kb = bytes / 1024D;
        if (kb < 1024)
            return kb.ToString("N1", CultureInfo.InvariantCulture) + " KB";

        var mb = kb / 1024D;
        if (mb < 1024)
            return mb.ToString("N2", CultureInfo.InvariantCulture) + " MB";

        var gb = mb / 1024D;
        return gb.ToString("N2", CultureInfo.InvariantCulture) + " GB";
    }

    private static void OpenFile(string filePath, string fileDescription)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("The file path is empty.");

            var folder = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(filePath))
                File.WriteAllText(filePath, "DeskPulse diagnostic log has not recorded entries yet." + Environment.NewLine);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {fileDescription} could not be opened.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void ShowActiveExtensions()
    {
        var settings = AppSettings.Load();
        var extensions = string.Join(Environment.NewLine, settings.ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        MessageBox.Show(
            "Active monitored extensions:" + Environment.NewLine + Environment.NewLine + extensions,
            "DeskPulse Diagnostics",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private static void OpenFolder(string folderPath, string folderDescription)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new InvalidOperationException("The folder path is empty.");

            Directory.CreateDirectory(folderPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The {folderDescription} could not be opened.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void RemoveRegistrySettings()
    {
        var confirm = MessageBox.Show(
            "This will remove DeskPulse settings from the Windows registry for the current user.\n\n" +
            "It will not delete the DeskPulse program files.\n" +
            "It will not delete the SQLite database or Excel export.\n\n" +
            "If you click OK in the Settings window afterwards, settings may be created again. " +
            "For a clean portable-app reset, close DeskPulse after removing the registry settings.\n\n" +
            "Continue?",
            "Remove DeskPulse Registry Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            AppSettings.DeleteRegistrySettings();

            MessageBox.Show(
                "DeskPulse registry settings were removed for the current Windows user.",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"DeskPulse registry settings could not be removed.\n\n{ex.Message}",
                "DeskPulse Maintenance",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void LoadExtensionLists(AppSettings settings)
    {
        var registered = RegisteredFileTypeReader.ReadRegisteredFileTypes();

        foreach (var item in registered)
            _availableExtensionsListBox.Items.Add(item);

        foreach (var extension in settings.ExtensionsToMonitor.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            _monitoredExtensionsListBox.Items.Add(extension);
    }

    private void LoadExportOptions(AppSettings settings)
    {
        _updatingExportUi = true;

        try
        {
            _exportSheetsCheckedListBox.Items.Clear();
            _exportFieldTabControl.TabPages.Clear();
            _exportFieldListsBySheetId.Clear();

            var allOptions = ExportSheetOption.GetAllOptions();
            var selectedOptions = ExportSheetOption.NormalizeList(settings.ExportSheets);
            var orderedOptions = new List<ExportSheetOption>();

            foreach (var selected in selectedOptions)
            {
                var template = allOptions.FirstOrDefault(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));

                if (template == null)
                    continue;

                orderedOptions.Add(new ExportSheetOption
                {
                    Id = template.Id,
                    DisplayName = template.DisplayName,
                    WorksheetName = template.WorksheetName,
                    FieldIds = selected.FieldIds.ToList()
                });
            }

            foreach (var option in allOptions)
            {
                if (orderedOptions.Any(x => string.Equals(x.Id, option.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                orderedOptions.Add(option);
            }

            foreach (var option in orderedOptions)
            {
                var selectedOption = selectedOptions.FirstOrDefault(x => string.Equals(x.Id, option.Id, StringComparison.OrdinalIgnoreCase));
                var item = selectedOption ?? option;
                var itemIndex = _exportSheetsCheckedListBox.Items.Add(item);
                _exportSheetsCheckedListBox.SetItemChecked(itemIndex, selectedOption != null);
            }
        }
        finally
        {
            _updatingExportUi = false;
        }

        RebuildExportFieldTabs();
    }

    private void RebuildExportFieldTabs()
    {
        if (_updatingExportUi)
            return;

        var previousSelectedSheetId = "";

        if (_exportFieldTabControl.SelectedTab?.Tag is string selectedSheetId)
            previousSelectedSheetId = selectedSheetId;

        var existingFieldSelections = ReadCurrentFieldSelections();

        _exportFieldTabControl.TabPages.Clear();
        _exportFieldListsBySheetId.Clear();

        foreach (var option in GetCheckedExportSheetItems())
        {
            var fieldList = new CheckedListBox
            {
                Left = 8,
                Top = 8,
                Width = 430,
                Height = 300,
                CheckOnClick = true,
                SelectionMode = SelectionMode.One,
                IntegralHeight = false,
                DisplayMember = nameof(ExportFieldOption.DisplayName)
            };

            var fieldIds = existingFieldSelections.TryGetValue(option.Id, out var savedFieldIds)
                ? savedFieldIds
                : option.FieldIds;

            var selectedFields = ExportSheetOption.NormalizeFieldList(option.Id, fieldIds);
            var availableFields = option.GetAvailableFields();
            var orderedFields = new List<ExportFieldOption>();

            foreach (var selectedField in selectedFields)
            {
                var field = availableFields.FirstOrDefault(x => string.Equals(x.Id, selectedField.Id, StringComparison.OrdinalIgnoreCase));

                if (field != null && !orderedFields.Any(x => string.Equals(x.Id, field.Id, StringComparison.OrdinalIgnoreCase)))
                    orderedFields.Add(field);
            }

            foreach (var availableField in availableFields)
            {
                if (!orderedFields.Any(x => string.Equals(x.Id, availableField.Id, StringComparison.OrdinalIgnoreCase)))
                    orderedFields.Add(availableField);
            }

            foreach (var field in orderedFields)
            {
                var checkedIndex = fieldList.Items.Add(field);
                var isChecked = selectedFields.Any(x => string.Equals(x.Id, field.Id, StringComparison.OrdinalIgnoreCase));
                fieldList.SetItemChecked(checkedIndex, isChecked);
            }

            var tabPage = new TabPage
            {
                Text = option.WorksheetName,
                Tag = option.Id
            };

            tabPage.Controls.Add(fieldList);
            _exportFieldTabControl.TabPages.Add(tabPage);
            _exportFieldListsBySheetId[option.Id] = fieldList;

            if (string.Equals(previousSelectedSheetId, option.Id, StringComparison.OrdinalIgnoreCase))
                _exportFieldTabControl.SelectedTab = tabPage;
        }
    }

    private List<ExportSheetOption> GetCheckedExportSheetItems()
    {
        var result = new List<ExportSheetOption>();

        for (var i = 0; i < _exportSheetsCheckedListBox.Items.Count; i++)
        {
            if (!_exportSheetsCheckedListBox.GetItemChecked(i))
                continue;

            if (_exportSheetsCheckedListBox.Items[i] is ExportSheetOption option)
                result.Add(option);
        }

        return result;
    }

    private Dictionary<string, List<string>> ReadCurrentFieldSelections()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in _exportFieldListsBySheetId)
            result[pair.Key] = GetCheckedFieldIds(pair.Value);

        return result;
    }

    private static List<string> GetCheckedFieldIds(CheckedListBox fieldList)
    {
        var result = new List<string>();

        for (var i = 0; i < fieldList.Items.Count; i++)
        {
            if (!fieldList.GetItemChecked(i))
                continue;

            if (fieldList.Items[i] is ExportFieldOption field)
                result.Add(field.Id);
        }

        return result;
    }

    private void MoveSelectedExportSheet(int direction)
    {
        var selectedIndex = _exportSheetsCheckedListBox.SelectedIndex;

        if (selectedIndex < 0)
            return;

        var newIndex = selectedIndex + direction;

        if (newIndex < 0 || newIndex >= _exportSheetsCheckedListBox.Items.Count)
            return;

        var item = _exportSheetsCheckedListBox.Items[selectedIndex];
        var wasChecked = _exportSheetsCheckedListBox.GetItemChecked(selectedIndex);
        var otherWasChecked = _exportSheetsCheckedListBox.GetItemChecked(newIndex);

        _updatingExportUi = true;

        try
        {
            _exportSheetsCheckedListBox.Items.RemoveAt(selectedIndex);
            _exportSheetsCheckedListBox.Items.Insert(newIndex, item);
            _exportSheetsCheckedListBox.SetItemChecked(newIndex, wasChecked);

            if (selectedIndex < _exportSheetsCheckedListBox.Items.Count)
                _exportSheetsCheckedListBox.SetItemChecked(selectedIndex, otherWasChecked);

            _exportSheetsCheckedListBox.SelectedIndex = newIndex;
        }
        finally
        {
            _updatingExportUi = false;
        }

        RebuildExportFieldTabs();
    }

    private void MoveSelectedExportField(int direction)
    {
        var fieldList = GetCurrentFieldList();

        if (fieldList == null)
            return;

        var selectedIndex = fieldList.SelectedIndex;

        if (selectedIndex < 0)
            return;

        var newIndex = selectedIndex + direction;

        if (newIndex < 0 || newIndex >= fieldList.Items.Count)
            return;

        var item = fieldList.Items[selectedIndex];
        var wasChecked = fieldList.GetItemChecked(selectedIndex);
        var otherWasChecked = fieldList.GetItemChecked(newIndex);

        fieldList.Items.RemoveAt(selectedIndex);
        fieldList.Items.Insert(newIndex, item);
        fieldList.SetItemChecked(newIndex, wasChecked);

        if (selectedIndex < fieldList.Items.Count)
            fieldList.SetItemChecked(selectedIndex, otherWasChecked);

        fieldList.SelectedIndex = newIndex;
    }

    private void SetCurrentExportFieldChecks(bool isChecked)
    {
        var fieldList = GetCurrentFieldList();

        if (fieldList == null)
            return;

        for (var i = 0; i < fieldList.Items.Count; i++)
            fieldList.SetItemChecked(i, isChecked);
    }

    private CheckedListBox? GetCurrentFieldList()
    {
        if (_exportFieldTabControl.SelectedTab?.Tag is not string sheetId)
            return null;

        return _exportFieldListsBySheetId.TryGetValue(sheetId, out var fieldList)
            ? fieldList
            : null;
    }

    private void ResetExportSheetsToDefault()
    {
        var resetSettings = new AppSettings
        {
            ExportSheets = ExportSheetOption.GetDefaultOptions()
        };

        LoadExportOptions(resetSettings);
    }

    private List<ExportSheetOption> GetSelectedExportSheetsFromList()
    {
        var result = new List<ExportSheetOption>();
        var currentFieldSelections = ReadCurrentFieldSelections();

        foreach (var option in GetCheckedExportSheetItems())
        {
            var fieldIds = currentFieldSelections.TryGetValue(option.Id, out var selectedFieldIds)
                ? selectedFieldIds
                : option.FieldIds;

            result.Add(new ExportSheetOption
            {
                Id = option.Id,
                DisplayName = option.DisplayName,
                WorksheetName = option.WorksheetName,
                FieldIds = fieldIds
            });
        }

        return ExportSheetOption.NormalizeList(result);
    }

    private void BrowseForDataFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the DeskPulse data folder",
            SelectedPath = Directory.Exists(_dataFolderTextBox.Text.Trim())
                ? _dataFolderTextBox.Text.Trim()
                : AppSettings.GetDefaultDataFolderPath(),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            _dataFolderTextBox.Text = dialog.SelectedPath;
    }

    private void AddSelectedExtension()
    {
        if (_availableExtensionsListBox.SelectedItem is RegisteredFileTypeInfo info)
            AddExtensionToMonitoredList(info.Extension);
    }

    private void RemoveSelectedExtension()
    {
        var selected = _monitoredExtensionsListBox.SelectedItem;

        if (selected != null)
            _monitoredExtensionsListBox.Items.Remove(selected);
    }

    private void AddExtensionToMonitoredList(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return;

        if (!extension.StartsWith(".", StringComparison.Ordinal))
            extension = "." + extension;

        extension = extension.ToLowerInvariant();

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            if (string.Equals(item.ToString(), extension, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _monitoredExtensionsListBox.Items.Add(extension);
    }

    private HashSet<string> GetMonitoredExtensionsFromList()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _monitoredExtensionsListBox.Items)
        {
            var text = item.ToString();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var extension = text.Trim();

            if (!extension.StartsWith(".", StringComparison.Ordinal))
                extension = "." + extension;

            result.Add(extension.ToLowerInvariant());
        }

        return result;
    }

    private void ListBoxMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        var index = listBox.IndexFromPoint(e.Location);

        if (index < 0)
            return;

        var item = listBox.Items[index];

        if (item == null)
            return;

        listBox.DoDragDrop(item, DragDropEffects.Move);
    }

    private void ListBoxDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && (e.Data.GetDataPresent(typeof(RegisteredFileTypeInfo)) || e.Data.GetDataPresent(typeof(string))))
        {
            e.Effect = DragDropEffects.Move;
            return;
        }

        e.Effect = DragDropEffects.None;
    }

    private void AddDraggedExtension(DragEventArgs e)
    {
        if (e.Data == null)
            return;

        if (e.Data.GetData(typeof(RegisteredFileTypeInfo)) is RegisteredFileTypeInfo info)
        {
            AddExtensionToMonitoredList(info.Extension);
            return;
        }

        if (e.Data.GetData(typeof(string)) is string text)
            AddExtensionToMonitoredList(text);
    }

    private void RemoveDraggedExtension(DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(string)) is not string text)
            return;

        foreach (var item in _monitoredExtensionsListBox.Items.Cast<object>().ToList())
        {
            if (string.Equals(item.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                _monitoredExtensionsListBox.Items.Remove(item);
                return;
            }
        }
    }

    private void SaveSettings()
    {
        if (_maintenanceOnly)
        {
            var maintenanceSettings = AppSettings.Load();
            var maintenanceLoggingRules = _loggingRulesGridView == null ? maintenanceSettings.LoggingRules : GetLoggingRulesFromGrid();

            maintenanceSettings.LoggingRules = maintenanceLoggingRules;
            maintenanceSettings.ExcludedFolders = ExtractFolderRules(maintenanceLoggingRules);
            maintenanceSettings.ExcludedProcesses = new HashSet<string>(ExtractProcessRules(maintenanceLoggingRules), StringComparer.OrdinalIgnoreCase);
            maintenanceSettings.Save();
            return;
        }

        var dataFolder = _dataFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(dataFolder))
        {
            MessageBox.Show(
                "Please enter a data folder.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        if (!Path.IsPathFullyQualified(dataFolder))
        {
            MessageBox.Show(
                "Please enter a full data folder path, for example:\n\nC:\\Users\\YourName\\Documents\\DeskPulse",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            Directory.CreateDirectory(dataFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The data folder could not be created or accessed.\n\n{ex.Message}",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            DialogResult = DialogResult.None;
            return;
        }

        var extensions = GetMonitoredExtensionsFromList();

        if (extensions.Count == 0)
        {
            MessageBox.Show(
                "Please add at least one monitored file extension.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var exportSheets = GetSelectedExportSheetsFromList();

        if (exportSheets.Count == 0)
        {
            MessageBox.Show(
                "Please select at least one Excel export worksheet.",
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            DialogResult = DialogResult.None;
            return;
        }

        var startWithWindows = _startWithWindowsCheckBox.Checked;

        try
        {
            StartupTaskManager.SetEnabled(startWithWindows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The Windows startup setting could not be saved.\n\n" + ex.Message,
                "Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            DialogResult = DialogResult.None;
            return;
        }

        var existingSettings = AppSettings.Load();
        var loggingRules = _loggingRulesGridView == null ? existingSettings.LoggingRules : GetLoggingRulesFromGrid();

        var settings = new AppSettings
        {
            DataFolderPath = dataFolder,
            IgnoreTempFolders = _ignoreTempFoldersCheckBox.Checked,
            StartWithWindows = startWithWindows,
            LogProgramActivity = _logProgramActivityCheckBox.Checked,
            LoggingRules = loggingRules,
            ExcludedFolders = ExtractFolderRules(loggingRules),
            ExcludedProcesses = new HashSet<string>(ExtractProcessRules(loggingRules), StringComparer.OrdinalIgnoreCase),
            ExtensionsToMonitor = extensions,
            ExportSheets = exportSheets
        };

        settings.Save();
    }
}
