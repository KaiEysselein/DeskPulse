using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
    private DataGridView _loggingRulesGridView = null!; // legacy designer control, no longer shown
    private ActivityRuleEditor _fileActivityRuleEditor = null!;
    private ActivityRuleEditor _userActivityRuleEditor = null!;
    private ActivityRuleEditor _appActivityRuleEditor = null!;
    private TextBox _manualLoggingRuleTextBox = null!;
    private RadioButton _manualRuleFolderRadioButton = null!;
    private RadioButton _manualRuleFileRadioButton = null!;
    private RadioButton _manualRuleProcessRadioButton = null!;
    private RadioButton _manualRuleExcludeRadioButton = null!;
    private RadioButton _manualRuleIncludeRadioButton = null!;
    private CheckBox _manualRuleSubfoldersCheckBox = null!;
    private DataGridView _statisticsGrid = null!;
    private ComboBox _statisticsViewComboBox = null!;
    private bool _updatingExportUi;
    private Button _importRulesButton = null!;
    private Button _exportRulesButton = null!;
    private TabPage _maintenanceHousekeepingTabPage = null!;
    private Button _cleanDatabaseWithRulesButton = null!;
    private Button _restartWindowsServiceButton = null!;
    private Label _windowsServiceStatusLabel = null!;
    private CheckBox _trackWindowsSystemActivityCheckBox = null!;
    private Button _configureFilteredFileActivityProcessesButton = null!;
    private Label _filteredFileActivityProcessesSummaryLabel = null!;
    private HashSet<string> _filteredFileActivityProcesses = new(StringComparer.OrdinalIgnoreCase);
    private bool _initializingUi = true;
    private readonly Button _saveAndCloseButton = new();
    private bool _isDirty;
    private bool _allowClose;

    public SettingsForm()
    {
        InitializeComponent();
        AppIcon.Apply(this);
        ConfigureActivityRuleTabs();
        ConfigureGeneralActivityLogging();
        ConfigureRuleImportExportButtons();
        ConfigureMaintenanceTab();

        Text = "DeskPulse Settings";

        _settingsTabControl.TabPages.Clear();
        _settingsTabControl.TabPages.Add(_generalTabPage);
        _settingsTabControl.TabPages.Add(_rulesTabPage);
        _settingsTabControl.TabPages.Add(_maintenanceHousekeepingTabPage);
        _settingsTabControl.SelectedIndexChanged += SettingsTabControl_SelectedIndexChanged;

        ConfigureSettingsFooterButtons();

        var settings = AppSettings.Load();
        LoadDesignerSettings(settings);
        LoadRuleSettings(settings);

        _initializingUi = false;
        HookDirtyTracking(this);
        _isDirty = false;
        UpdateFooterForSelectedTab();
        FormClosing += SettingsForm_FormClosing;
    }

    private void ConfigureSettingsFooterButtons()
    {
        _saveButton.DialogResult = DialogResult.None;
        _saveButton.Text = "Save";
        _saveButton.Location = new System.Drawing.Point(584, 644);
        _saveButton.Size = new System.Drawing.Size(84, 30);

        _saveAndCloseButton.Text = "Save and Close";
        _saveAndCloseButton.FlatStyle = FlatStyle.System;
        _saveAndCloseButton.Location = new System.Drawing.Point(680, 644);
        _saveAndCloseButton.Size = new System.Drawing.Size(116, 30);
        _saveAndCloseButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _saveAndCloseButton.Click += SaveAndCloseButton_Click;
        Controls.Add(_saveAndCloseButton);
        _saveAndCloseButton.BringToFront();

        _cancelButton.DialogResult = DialogResult.None;
        _cancelButton.Text = "Close";
        _cancelButton.Location = new System.Drawing.Point(808, 644);
        _cancelButton.Size = new System.Drawing.Size(96, 30);
        _cancelButton.Click += (_, _) => Close();

        AcceptButton = _saveAndCloseButton;
        CancelButton = _cancelButton;
    }

    private void HookDirtyTracking(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TextBoxBase textBox:
                    textBox.TextChanged += (_, _) => MarkDirty();
                    break;
                case CheckBox checkBox:
                    checkBox.CheckedChanged += (_, _) => MarkDirty();
                    break;
                case RadioButton radioButton:
                    radioButton.CheckedChanged += (_, _) => MarkDirty();
                    break;
                case ComboBox comboBox:
                    comboBox.SelectedIndexChanged += (_, _) => MarkDirty();
                    break;
                case CheckedListBox checkedList:
                    checkedList.ItemCheck += (_, _) => BeginInvoke(new Action(MarkDirty));
                    break;
                case DataGridView grid:
                    grid.CellValueChanged += (_, _) => MarkDirty();
                    grid.RowsAdded += (_, _) => MarkDirty();
                    grid.RowsRemoved += (_, _) => MarkDirty();
                    break;
            }

            if (control.HasChildren)
                HookDirtyTracking(control);
        }
    }

    private void MarkDirty()
    {
        if (_initializingUi)
            return;

        _isDirty = true;
        UpdateFooterForSelectedTab();
    }

    private void SaveAndCloseButton_Click(object? sender, EventArgs e)
    {
        if (!SaveSettings())
            return;

        _allowClose = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SettingsForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose || !_isDirty)
            return;

        var discard = MessageBox.Show(
            "Close DeskPulse Settings and discard unsaved changes?",
            "Unsaved Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (discard != DialogResult.Yes)
            e.Cancel = true;
        else
            _allowClose = true;
    }

    private void SettingsTabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateFooterForSelectedTab();
    }

    private void UpdateFooterForSelectedTab()
    {
        var isRulesTab = ReferenceEquals(_settingsTabControl.SelectedTab, _rulesTabPage);
        var isMaintenanceTab = ReferenceEquals(_settingsTabControl.SelectedTab, _maintenanceHousekeepingTabPage);

        _importRulesButton.Visible = isRulesTab;
        _exportRulesButton.Visible = isRulesTab;

        _saveButton.Visible = !isMaintenanceTab;
        _saveAndCloseButton.Visible = !isMaintenanceTab;
        _saveButton.Enabled = !isMaintenanceTab && _isDirty;
        _saveAndCloseButton.Enabled = !isMaintenanceTab && _isDirty;
        _cancelButton.Text = "Close";
        _cancelButton.Enabled = true;

        AcceptButton = isMaintenanceTab ? null : _saveAndCloseButton;
        CancelButton = _cancelButton;
    }

    private void ConfigureGeneralActivityLogging()
    {
        _behaviourGroupBox.Text = "Activity logging";
        _behaviourGroupBox.Height = 230;
        _storageGroupBox.Top = 448;

        _logProgramActivityCheckBox.Location = new System.Drawing.Point(18, 30);
        _programActivityHintLabel.Location = new System.Drawing.Point(40, 54);
        _programActivityHintLabel.Height = 32;

        _configureFilteredFileActivityProcessesButton = new Button
        {
            Text = "Configure filtered file applications...",
            Left = 18,
            Top = 94,
            Width = 270,
            Height = 30,
            FlatStyle = FlatStyle.System
        };
        _configureFilteredFileActivityProcessesButton.Click += ConfigureFilteredFileActivityProcessesButton_Click;

        _filteredFileActivityProcessesSummaryLabel = new Label
        {
            Left = 304,
            Top = 101,
            Width = 480,
            Height = 22,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        var processHint = new Label
        {
            Left = 40,
            Top = 130,
            Width = 740,
            Height = 34,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Text = "Applications moved to the filtered list do not create new File Activity records. Database housekeeping also removes their existing File Activity records."
        };

        _trackWindowsSystemActivityCheckBox = new CheckBox
        {
            Text = "Track Windows system activity",
            Left = 18,
            Top = 170,
            Width = 360,
            Height = 22
        };
        var windowsHint = new Label
        {
            Left = 40,
            Top = 194,
            Width = 740,
            Height = 30,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Text = "When disabled, DeskPulse suppresses routine Windows files, folders, services, and background processes."
        };
        _trackWindowsSystemActivityCheckBox.CheckedChanged += (_, _) =>
        {
            var active = !_trackWindowsSystemActivityCheckBox.Checked;
            _fileActivityRuleEditor.SetWindowsDefaultRules(WindowsDefaultExclusions.GetFileRules(), active);
            _appActivityRuleEditor.SetWindowsDefaultRules(WindowsDefaultExclusions.GetProcessRules(), active);
        };

        _behaviourHintLabel.Visible = false;
        _behaviourGroupBox.Controls.Add(_configureFilteredFileActivityProcessesButton);
        _behaviourGroupBox.Controls.Add(_filteredFileActivityProcessesSummaryLabel);
        _behaviourGroupBox.Controls.Add(processHint);
        _behaviourGroupBox.Controls.Add(_trackWindowsSystemActivityCheckBox);
        _behaviourGroupBox.Controls.Add(windowsHint);
    }

    private void ConfigureFilteredFileActivityProcessesButton_Click(object? sender, EventArgs e)
    {
        var databasePath = Path.Combine(_dataFolderTextBox.Text.Trim(), "DeskPulse.db");
        IReadOnlyList<FileActivityProcessSummary> summaries;
        try
        {
            using var database = new DeskPulseDatabase(databasePath, readOnly: true);
            summaries = database.GetFileActivityProcessSummaries();
        }
        catch (Exception ex)
        {
            MessageBox.Show("The existing File Activity applications could not be read. You can still add process names manually.\n\n" + ex.Message,
                "Filtered File Applications", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            summaries = Array.Empty<FileActivityProcessSummary>();
        }

        using var dialog = new FilteredFileActivityProcessesForm(summaries, _filteredFileActivityProcesses);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _filteredFileActivityProcesses = new HashSet<string>(dialog.FilteredProcesses, StringComparer.OrdinalIgnoreCase);
        UpdateFilteredProcessSummary();
        MarkDirty();
    }

    private void UpdateFilteredProcessSummary()
    {
        var count = _filteredFileActivityProcesses.Count;
        _filteredFileActivityProcessesSummaryLabel.Text = count == 0
            ? "No applications filtered"
            : count.ToString("N0", CultureInfo.InvariantCulture) + (count == 1 ? " application filtered" : " applications filtered");
    }

    private void ConfigureActivityRuleTabs()
    {
        _rulesSubTabControl.TabPages.Clear();

        var filePage = new TabPage("File Activity") { BackColor = System.Drawing.SystemColors.Window, Padding = new Padding(8) };
        var appPage = new TabPage("App Activity") { BackColor = System.Drawing.SystemColors.Window, Padding = new Padding(8) };
        var userPage = new TabPage("User Activity") { BackColor = System.Drawing.SystemColors.Window, Padding = new Padding(8) };

        _fileActivityRuleEditor = new ActivityRuleEditor(ActivityRuleCategory.File) { Dock = DockStyle.Fill };
        _appActivityRuleEditor = new ActivityRuleEditor(ActivityRuleCategory.App) { Dock = DockStyle.Fill };
        _userActivityRuleEditor = new ActivityRuleEditor(ActivityRuleCategory.User) { Dock = DockStyle.Fill };

        filePage.Controls.Add(_fileActivityRuleEditor);
        appPage.Controls.Add(_appActivityRuleEditor);
        userPage.Controls.Add(_userActivityRuleEditor);

        _rulesSubTabControl.TabPages.Add(filePage);
        _rulesSubTabControl.TabPages.Add(appPage);
        _rulesSubTabControl.TabPages.Add(userPage);
    }

    private void ConfigureRuleImportExportButtons()
    {
        _importRulesButton = new Button
        {
            Text = "Import Rules...",
            Location = new System.Drawing.Point(24, 644),
            Size = new System.Drawing.Size(112, 30),
            FlatStyle = FlatStyle.System,
            TabIndex = 20
        };
        _importRulesButton.Click += ImportRulesButton_Click;

        _exportRulesButton = new Button
        {
            Text = "Export Rules...",
            Location = new System.Drawing.Point(144, 644),
            Size = new System.Drawing.Size(112, 30),
            FlatStyle = FlatStyle.System,
            TabIndex = 21
        };
        _exportRulesButton.Click += ExportRulesButton_Click;

        Controls.Add(_importRulesButton);
        Controls.Add(_exportRulesButton);
        _importRulesButton.BringToFront();
        _exportRulesButton.BringToFront();
    }


    private void ConfigureMaintenanceTab()
    {
        _maintenanceHousekeepingTabPage = new TabPage
        {
            Text = "Maintenance",
            BackColor = System.Drawing.SystemColors.Window,
            Padding = new Padding(16),
            AutoScroll = true
        };

        var housekeepingGroup = new GroupBox
        {
            Text = "Database housekeeping",
            Left = 16,
            Top = 16,
            Width = 820,
            Height = 150,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Left = 16,
            Top = 28,
            Width = 785,
            Height = 54,
            Text = "Apply the current File, App, and User Activity rules and active logging filters to the entire database history. " +
                   "Records that would not be logged under the current settings are permanently removed, after which the database is compacted."
        };

        _cleanDatabaseWithRulesButton = new Button
        {
            Text = "Clean database with current rules...",
            Left = 16,
            Top = 94,
            Width = 250,
            Height = 32,
            FlatStyle = FlatStyle.System
        };
        _cleanDatabaseWithRulesButton.Click += CleanDatabaseWithCurrentRulesButton_Click;

        housekeepingGroup.Controls.Add(descriptionLabel);
        housekeepingGroup.Controls.Add(_cleanDatabaseWithRulesButton);
        _maintenanceHousekeepingTabPage.Controls.Add(housekeepingGroup);

        var serviceGroup = new GroupBox
        {
            Text = "Windows service",
            Left = 16,
            Top = 182,
            Width = 820,
            Height = 130,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var serviceDescriptionLabel = new Label
        {
            AutoSize = false,
            Left = 16,
            Top = 28,
            Width = 785,
            Height = 38,
            Text = "Restart the DeskPulse background service if monitoring has stopped, performance has degraded, or the service is not responding. Windows will request administrator approval."
        };

        _restartWindowsServiceButton = new Button
        {
            Text = "Restart Windows Service",
            Left = 16,
            Top = 78,
            Width = 210,
            Height = 32,
            FlatStyle = FlatStyle.System
        };
        _restartWindowsServiceButton.Click += RestartWindowsServiceButton_Click;

        _windowsServiceStatusLabel = new Label
        {
            AutoSize = false,
            Left = 240,
            Top = 84,
            Width = 560,
            Height = 24,
            Text = "Service status will be checked after restart."
        };

        serviceGroup.Controls.Add(serviceDescriptionLabel);
        serviceGroup.Controls.Add(_restartWindowsServiceButton);
        serviceGroup.Controls.Add(_windowsServiceStatusLabel);
        _maintenanceHousekeepingTabPage.Controls.Add(serviceGroup);
    }

    private async void RestartWindowsServiceButton_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart the DeskPulse Windows service now?\n\nLogging will pause briefly while the service stops and starts again. Windows will ask for administrator approval.",
            "Restart DeskPulse Service",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        _restartWindowsServiceButton.Enabled = false;
        _windowsServiceStatusLabel.Text = "Restarting DeskPulse service...";
        Cursor = Cursors.WaitCursor;

        try
        {
            var command =
                "$ErrorActionPreference='Stop'; " +
                "Restart-Service -Name 'DeskPulse.Service' -Force; " +
                "$service = Get-Service -Name 'DeskPulse.Service'; " +
                "$service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Windows could not start the elevated service restart command.");

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("The service restart command failed with exit code " + process.ExitCode + ".");

            string status = "DeskPulse service is restarting.";
            for (var attempt = 0; attempt < 10; attempt++)
            {
                await Task.Delay(500);
                status = await ServicePipeClient.GetStatusAsync();
                if (status.StartsWith("DeskPulse service is running", StringComparison.OrdinalIgnoreCase))
                    break;
            }

            _windowsServiceStatusLabel.Text = status;
            MessageBox.Show(status, "DeskPulse Service", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _windowsServiceStatusLabel.Text = "Restart cancelled by user.";
        }
        catch (Exception ex)
        {
            _windowsServiceStatusLabel.Text = "Service restart failed.";
            MessageBox.Show(
                "DeskPulse could not restart the Windows service.\n\n" + ex.Message,
                "Restart DeskPulse Service",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            _restartWindowsServiceButton.Enabled = true;
        }
    }

    private void CleanDatabaseWithCurrentRulesButton_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "This will permanently remove every historical DeskPulse record that conflicts with the rules currently displayed in Settings.\n\n" +
            "The cleanup applies the current File and App Activity rules, Windows-system setting, and filtered File Activity applications, and then compacts the SQLite database.\n\n" +
            "The current rules will also be saved before cleanup begins. Deleted records cannot be recovered unless you have a database backup.\n\nContinue?",
            "Clean Database With Current Rules",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        var settings = BuildSettingsFromCurrentControls();
        if (settings == null)
            return;

        settings.Save();

        using var progressForm = new RuleCleanupProgressForm(
            "DeskPulse Database Housekeeping",
            "Applying current rules through the DeskPulse service",
            progress => ServicePipeClient.RunDatabaseHousekeepingAsync(progress).GetAwaiter().GetResult());

        if (progressForm.ShowDialog(this) != DialogResult.OK || progressForm.Result == null)
            return;

        var result = progressForm.Result;
        MessageBox.Show(
            "Database housekeeping completed.\n\n" +
            "File activity records removed: " + result.ActivityRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
            "App activity records removed: " + result.ProgramRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
            "User activity records removed: " + result.UserRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
            "Total records removed: " + result.TotalRecordsDeleted.ToString("N0", CultureInfo.InvariantCulture),
            "DeskPulse Maintenance",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private AppSettings? BuildSettingsFromCurrentControls()
    {
        var dataFolder = _dataFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(dataFolder) || !Path.IsPathFullyQualified(dataFolder))
        {
            MessageBox.Show("Please enter a valid full data folder path before running maintenance.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var existingSettings = AppSettings.Load();
        var exportSheets = existingSettings.ExportSheets.Count > 0
            ? existingSettings.ExportSheets
            : ExportSheetOption.GetDefaultOptions();
        var appActivityRuleSettings = _appActivityRuleEditor.GetRuleSettings();
        var fileActivityRuleSettings = RemoveFileRulesDuplicatedByAppRules(_fileActivityRuleEditor.GetRuleSettings(), appActivityRuleSettings);
        var userActivityRuleSettings = _userActivityRuleEditor.GetRuleSettings();

        var fileActivityRules = fileActivityRuleSettings.Where(rule => rule.Enabled).Select(rule => rule.ToRuleText()).ToList();
        var appActivityRules = appActivityRuleSettings.Where(rule => rule.Enabled).Select(rule => rule.ToRuleText()).ToList();

        return new AppSettings
        {
            DataFolderPath = dataFolder,
            IgnoreTempFolders = _ignoreTempFoldersCheckBox.Checked,
            StartWithWindows = _startWithWindowsCheckBox.Checked,
            LogProgramActivity = _logProgramActivityCheckBox.Checked,
            LogExplorerFileActivity = true,
            FilteredFileActivityProcesses = new HashSet<string>(_filteredFileActivityProcesses, StringComparer.OrdinalIgnoreCase),
            TrackWindowsSystemActivity = _trackWindowsSystemActivityCheckBox.Checked,
            LoggingRules = fileActivityRules.Concat(appActivityRules).ToList(),
            FileActivityRuleSettings = fileActivityRuleSettings,
            FolderActivityRuleSettings = new List<ActivityRuleSetting>(),
            UserActivityRuleSettings = userActivityRuleSettings,
            AppActivityRuleSettings = appActivityRuleSettings,
            ExcludedFolders = new List<string>(),
            ExcludedProcesses = new HashSet<string>(ExtractProcessRules(appActivityRules), StringComparer.OrdinalIgnoreCase),
            ExtensionsToMonitor = existingSettings.ExtensionsToMonitor,
            ExportSheets = exportSheets
        };
    }

    private void ExportRulesButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export DeskPulse rules",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"DeskPulse-rules-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var package = new RuleSettingsExportPackage
            {
                SchemaVersion = 2,
                DeskPulseVersion = AppInfo.Version,
                ExportedAt = DateTime.Now,
                FileActivity = _fileActivityRuleEditor.GetRuleSettings(),
                AppActivity = _appActivityRuleEditor.GetRuleSettings()
            };

            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show("The File and App Activity rules were exported successfully.", "Export Rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The rules could not be exported.\n\n" + ex.Message, "Export Rules", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportRulesButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import DeskPulse rules",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var package = JsonSerializer.Deserialize<RuleSettingsExportPackage>(
                File.ReadAllText(dialog.FileName),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (package == null || (package.SchemaVersion != 1 && package.SchemaVersion != 2))
                throw new InvalidDataException("The selected file is not a supported DeskPulse rules export.");

            package.FileActivity ??= new List<ActivityRuleSetting>();
            package.FolderActivity ??= new List<ActivityRuleSetting>();
            package.AppActivity ??= new List<ActivityRuleSetting>();

            var invalid = package.FileActivity.Any(rule => !string.Equals(rule.RuleType, "file", StringComparison.OrdinalIgnoreCase))
                || package.FolderActivity.Any(rule => !string.Equals(rule.RuleType, "folder", StringComparison.OrdinalIgnoreCase))
                || package.AppActivity.Any(rule => !string.Equals(rule.RuleType, "process", StringComparison.OrdinalIgnoreCase));

            if (invalid)
                throw new InvalidDataException("The file contains rules assigned to the wrong activity category.");

            var importedFileRules = AppSettings.MergeFolderRulesIntoFileRules(package.FileActivity, package.FolderActivity);

            using var modeDialog = new RuleImportModeForm();
            if (modeDialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (modeDialog.ImportMode == RuleImportMode.Replace)
            {
                _fileActivityRuleEditor.LoadRuleSettings(importedFileRules);
                _appActivityRuleEditor.LoadRuleSettings(package.AppActivity);
            }
            else
            {
                _fileActivityRuleEditor.LoadRuleSettings(MergeImportedRules(
                    _fileActivityRuleEditor.GetRuleSettings(), importedFileRules));
                _appActivityRuleEditor.LoadRuleSettings(MergeImportedRules(
                    _appActivityRuleEditor.GetRuleSettings(), package.AppActivity));
            }

            var action = modeDialog.ImportMode == RuleImportMode.Replace ? "replaced" : "merged";
            MessageBox.Show(
                $"The File and App Activity rules were {action} in Settings. User Activity rules were unchanged.\n\nClick Save to apply the imported rules and write them to the registry.",
                "Import Rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The rules could not be imported.\n\n" + ex.Message, "Import Rules", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<ActivityRuleSetting> MergeImportedRules(
        IReadOnlyList<ActivityRuleSetting> currentRules,
        IReadOnlyList<ActivityRuleSetting> importedRules)
    {
        var merged = currentRules.Select(rule => rule.Clone()).ToList();
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < merged.Count; i++)
            indexes[GetRuleIdentity(merged[i])] = i;

        foreach (var importedRule in importedRules)
        {
            var clone = importedRule.Clone();
            var identity = GetRuleIdentity(clone);

            if (indexes.TryGetValue(identity, out var existingIndex))
            {
                // Imported settings win for a matching rule, while its current list position is retained.
                merged[existingIndex] = clone;
            }
            else
            {
                indexes[identity] = merged.Count;
                merged.Add(clone);
            }
        }

        return merged;
    }

    private static string GetRuleIdentity(ActivityRuleSetting rule)
    {
        var ruleType = (rule.RuleType ?? string.Empty).Trim().ToLowerInvariant();
        var value = (rule.Value ?? string.Empty).Trim();

        if (ruleType.Equals("folder", StringComparison.OrdinalIgnoreCase))
        {
            value = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try
            {
                value = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                // Keep the normalized text when the path cannot be resolved on this computer.
            }
        }
        else if (ruleType.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            value = Path.GetFileName(value);
        }

        return $"{ruleType}|{value}";
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
        _filteredFileActivityProcesses = new HashSet<string>(settings.FilteredFileActivityProcesses ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
        if (!settings.LogExplorerFileActivity)
            _filteredFileActivityProcesses.Add("explorer.exe");
        UpdateFilteredProcessSummary();
        _trackWindowsSystemActivityCheckBox.Checked = settings.TrackWindowsSystemActivity;
        _dataFolderTextBox.Text = settings.DataFolderPath;
        _ignoreTempFoldersCheckBox.Checked = settings.IgnoreTempFolders;

        LoadExtensionLists(settings);
    }


    private void LoadRuleSettings(AppSettings settings)
    {
        var appRuleSettings = settings.AppActivityRuleSettings;
        var mergedFileRules = AppSettings.MergeFolderRulesIntoFileRules(settings.FileActivityRuleSettings, settings.FolderActivityRuleSettings);
        var fileRuleSettings = RemoveFileRulesDuplicatedByAppRules(mergedFileRules, appRuleSettings);

        _fileActivityRuleEditor.LoadRuleSettings(fileRuleSettings);
        _userActivityRuleEditor.LoadRuleSettings(settings.UserActivityRuleSettings);
        _appActivityRuleEditor.LoadRuleSettings(appRuleSettings);
        _trackWindowsSystemActivityCheckBox.Checked = settings.TrackWindowsSystemActivity;
        _fileActivityRuleEditor.SetWindowsDefaultRules(WindowsDefaultExclusions.GetFileRules(), !settings.TrackWindowsSystemActivity);
        _appActivityRuleEditor.SetWindowsDefaultRules(WindowsDefaultExclusions.GetProcessRules(), !settings.TrackWindowsSystemActivity);

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
        SetButtonToolTip(_maintenanceDeleteStartupLogButton, "Deletes only the temporary DeskPulse startup fallback log. Does not delete database records, settings, or logging rules.");
        SetButtonToolTip(_maintenanceRemoveUnwantedDataButton, "Deletes only past file/program records that match current Exclude rules in Logging Rules. Does not delete Include exceptions, the rules themselves, user/session records, settings, or the database file.");
        SetButtonToolTip(_maintenanceDeleteFileActivityButton, "Deletes ALL file open/write/close records from ActivityEvents. Keeps user/session records, program records, settings, logging rules, and the database file.");
        SetButtonToolTip(_maintenanceDeleteUserActivityButton, "Deletes ALL user/session records such as DeskPulse start/stop, PC lock/unlock, logon/logoff, and session connect/disconnect events. Keeps file/program records, settings, and logging rules.");
        SetButtonToolTip(_maintenanceDeleteProgramActivityButton, "Deletes ALL program start/close records from ProgramEvents. Keeps file activity, user/session records, settings, and logging rules.");
        SetButtonToolTip(_maintenanceDeleteAllActivityButton, "Deletes EVERYTHING recorded in DeskPulse activity tables: file activity, user/session activity, and program activity. Keeps settings, logging rules, and database structure.");
        SetButtonToolTip(_maintenanceBrowseRuleButton, "Browses for the folder, exact file, or program executable used for a new logging rule. Does not add or delete anything by itself.");
        SetButtonToolTip(_maintenanceAddRuleButton, "Adds the newly entered Include or Exclude rule to the rules list. It affects future logging after Save; it does not delete past records.");
        SetButtonToolTip(_maintenanceMoveRuleUpButton, "Moves the selected logging rule up. Higher rules override lower rules. Does not delete data.");
        SetButtonToolTip(_maintenanceMoveRuleDownButton, "Moves the selected logging rule down. Higher rules override lower rules. Does not delete data.");
        SetButtonToolTip(_maintenanceRemoveRuleButton, "Removes only the selected logging rule from the list. It does not delete past database records.");
        SetButtonToolTip(_maintenanceDuplicateRuleButton, "Copies the selected logging rule so it can be edited or reordered. Does not delete data.");
        SetButtonToolTip(_maintenanceRemovePastRecordsButton, "PERMANENTLY deletes past file/program records that match current Exclude rules. Does not delete the rules themselves or user/session records.");
        SetButtonToolTip(_maintenanceShowActiveExtensionsButton, "Shows the file extensions currently monitored by DeskPulse. Does not delete or change data.");
        SetButtonToolTip(_maintenanceShowStartupStatusButton, "Shows whether the current-user Windows startup registry entry exists. Does not delete or change data.");
        SetButtonToolTip(_maintenanceRemoveRegistrySettingsButton, "Deletes current-user DeskPulse registry settings, including preferences and logging rules. Does not delete the database, export, startup fallback log, or program files.");
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
    private void MaintenanceDeleteStartupLogButton_Click(object? sender, EventArgs e) => DeleteGeneratedFile(Path.Combine(Path.GetTempPath(), "DeskPulse-startup.log"), "startup fallback log");
    private void MaintenanceRepairHistoricalDataButton_Click(object? sender, EventArgs e) => RepairHistoricalData();
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
    private void MaintenanceRemovePastRecordsButton_Click(object? sender, EventArgs e) => RemoveExcludedPastRecords(AppSettings.Load());
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
        _startWithWindowsCheckBox.Text = "Start DeskPulse Tray when I log in to Windows";
        _startWithWindowsCheckBox.Checked = settings.StartWithWindows || StartupTaskManager.IsEnabled();

        var startupHintLabel = CreateHintLabel(
            "Starts the non-elevated DeskPulse Tray for your Windows account when you log in. The background service starts separately with Windows.",
            40,
            62,
            740,
            22);

        var quietStartupLabel = CreateHintLabel(
            "The tray starts quietly. The DeskPulse Windows service continues running independently in the background.",
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
        _filteredFileActivityProcesses = new HashSet<string>(settings.FilteredFileActivityProcesses ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
        if (!settings.LogExplorerFileActivity)
            _filteredFileActivityProcesses.Add("explorer.exe");
        UpdateFilteredProcessSummary();
        _trackWindowsSystemActivityCheckBox.Checked = settings.TrackWindowsSystemActivity;

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

    }


    private static string GetDatabaseOverviewForMaintenance(AppSettings settings)
    {
        try
        {
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath, readOnly: true);
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
            using var database = new DeskPulseDatabase(settings.DatabaseFilePath, readOnly: true);

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
                "Open Rules > Logging Rules first, then add the selected exclusion items.",
                "DeskPulse Settings",
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
                            "DeskPulse Settings",
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
                "No valid rules were added from the selected exclusion rows.",
                "DeskPulse Settings",
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
            " in Settings. Move them up/down if needed, then click Save to store the change." + skippedText,
            "DeskPulse Settings",
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
        var loggingRules = settings.FileActivityRules
            .Concat(settings.AppActivityRules)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        using var progressForm = new RuleCleanupProgressForm(
            "Remove Matching Past Records",
            "Removing excluded past records",
            progress =>
            {
                progress.Report(new ExportProgressInfo(8, "8%   Sending cleanup request to DeskPulse service"));
                return ServicePipeClient.RunDatabaseHousekeepingAsync().GetAwaiter().GetResult();
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

    private void RepairHistoricalData()
    {
        var confirm = MessageBox.Show(
            "DeskPulse will scan existing File Activity records and repair only recognized, recoverable data-format problems.\n\n" +
            "The current repair corrects mapped-drive LanmanRedirector paths and refreshes the related folder, file name, extension, and legacy item fields. Unrecognized or uncertain values are left unchanged.\n\n" +
            "A database backup is recommended before any maintenance operation.\n\nContinue?",
            "Repair Historical Data",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (confirm != DialogResult.Yes)
            return;

        using var progressForm = new RuleCleanupProgressForm(
            "Repair Historical Data",
            "Scanning and repairing historical records",
            (progress, cancellationToken) =>
                ServicePipeClient.RepairHistoricalDataAsync(progress, cancellationToken).GetAwaiter().GetResult());

        if (progressForm.ShowDialog(this) != DialogResult.OK || progressForm.Result == null)
            return;

        MessageBox.Show(
            progressForm.Result.ActivityRecordsRepaired.ToString("N0", CultureInfo.InvariantCulture) +
            " File Activity record(s) repaired.\n\nRecords without a recognized safe correction were not changed.",
            "DeskPulse Maintenance",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        _maintenanceDatabaseInfoTextBox.Text = GetDatabaseOverviewForMaintenance(AppSettings.Load());
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
            var deleted = ServicePipeClient.ClearTableAsync(tableName).GetAwaiter().GetResult();

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
            var deleted = ServicePipeClient.ClearAllRecordsAsync().GetAwaiter().GetResult();

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
            "Current-user tray startup: " + (enabled ? "Enabled" : "Not detected") + Environment.NewLine +
            "Registry location: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" + Environment.NewLine +
            "Value name: DeskPulse.Tray" + Environment.NewLine +
            "The DeskPulse Windows service starts independently as an automatic service.",
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

    private bool SaveSettings()
    {
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
            return false;
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
            return false;
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
            return false;
        }

        var existingSettings = AppSettings.Load();
        var exportSheets = existingSettings.ExportSheets.Count > 0
            ? existingSettings.ExportSheets
            : ExportSheetOption.GetDefaultOptions();

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
            return false;
        }

        var appActivityRuleSettings = _appActivityRuleEditor.GetRuleSettings();
        var fileActivityRuleSettings = RemoveFileRulesDuplicatedByAppRules(
            _fileActivityRuleEditor.GetRuleSettings(),
            appActivityRuleSettings);
        var userActivityRuleSettings = _userActivityRuleEditor.GetRuleSettings();

        var fileActivityRules = fileActivityRuleSettings.Where(rule => rule.Enabled).Select(rule => rule.ToRuleText()).ToList();
        var appActivityRules = appActivityRuleSettings.Where(rule => rule.Enabled).Select(rule => rule.ToRuleText()).ToList();

        var settings = new AppSettings
        {
            DataFolderPath = dataFolder,
            IgnoreTempFolders = _ignoreTempFoldersCheckBox.Checked,
            StartWithWindows = startWithWindows,
            LogProgramActivity = _logProgramActivityCheckBox.Checked,
            LogExplorerFileActivity = true,
            FilteredFileActivityProcesses = new HashSet<string>(_filteredFileActivityProcesses, StringComparer.OrdinalIgnoreCase),
            TrackWindowsSystemActivity = _trackWindowsSystemActivityCheckBox.Checked,
            LoggingRules = fileActivityRules.Concat(appActivityRules).ToList(),
            FileActivityRuleSettings = fileActivityRuleSettings,
            FolderActivityRuleSettings = new List<ActivityRuleSetting>(),
            UserActivityRuleSettings = userActivityRuleSettings,
            AppActivityRuleSettings = appActivityRuleSettings,
            ExcludedFolders = new List<string>(),
            ExcludedProcesses = new HashSet<string>(ExtractProcessRules(appActivityRules), StringComparer.OrdinalIgnoreCase),
            ExtensionsToMonitor = existingSettings.ExtensionsToMonitor,
            ExportSheets = exportSheets
        };

        settings.Save();
        _ = ServicePipeClient.SendAsync("RELOAD_SETTINGS");
        _isDirty = false;
        DialogResult = DialogResult.None;
        UpdateFooterForSelectedTab();
        return true;
    }

    private static List<ActivityRuleSetting> RemoveFileRulesDuplicatedByAppRules(
        IEnumerable<ActivityRuleSetting> fileRules,
        IEnumerable<ActivityRuleSetting> appRules)
    {
        var appKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var appRule in appRules ?? Array.Empty<ActivityRuleSetting>())
        {
            if (!appRule.Enabled || !appRule.RuleType.Equals("process", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = GetExecutableRuleKey(appRule.Value);
            if (key.Length > 0)
                appKeys.Add(key);
        }

        return (fileRules ?? Array.Empty<ActivityRuleSetting>())
            .Where(rule =>
            {
                if (!rule.Enabled || !rule.RuleType.Equals("file", StringComparison.OrdinalIgnoreCase))
                    return true;

                var value = rule.Value?.Trim() ?? "";
                if (value.IndexOfAny(new[] { '*', '?' }) >= 0)
                    return true;

                var key = GetExecutableRuleKey(value);
                return key.Length == 0 || !appKeys.Contains(key);
            })
            .ToList();
    }

    private static string GetExecutableRuleKey(string? value)
    {
        var text = (value ?? "").Trim().Trim('"');
        if (text.Length == 0)
            return "";

        string fileName;
        try
        {
            fileName = Path.GetFileName(text);
        }
        catch
        {
            fileName = text;
        }

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = text;

        if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return "";

        return fileName[..^4].Trim();
    }
}

internal enum RuleImportMode
{
    Merge,
    Replace
}

internal sealed class RuleImportModeForm : Form
{
    private readonly RadioButton _mergeRadioButton;
    private readonly RadioButton _replaceRadioButton;

    public RuleImportMode ImportMode => _replaceRadioButton.Checked
        ? RuleImportMode.Replace
        : RuleImportMode.Merge;

    public RuleImportModeForm()
    {
        Text = "Import Rules";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(470, 245);

        var heading = new Label
        {
            Left = 18,
            Top = 16,
            Width = 430,
            Height = 25,
            Text = "How should the imported rules be applied?",
            Font = new Font(Font, FontStyle.Bold)
        };

        _mergeRadioButton = new RadioButton
        {
            Left = 22,
            Top = 55,
            Width = 420,
            Height = 24,
            Checked = true,
            Text = "Merge with existing rules (recommended)"
        };

        var mergeDescription = new Label
        {
            Left = 46,
            Top = 79,
            Width = 390,
            Height = 38,
            Text = "Keeps existing rules, adds new rules, and updates matching rules without creating duplicates."
        };

        _replaceRadioButton = new RadioButton
        {
            Left = 22,
            Top = 125,
            Width = 420,
            Height = 24,
            Text = "Replace existing rules"
        };

        var replaceDescription = new Label
        {
            Left = 46,
            Top = 149,
            Width = 390,
            Height = 32,
            Text = "Replaces the complete File Activity and App Activity rule lists currently shown in Settings."
        };

        var importButton = new Button
        {
            Text = "Import",
            DialogResult = DialogResult.OK,
            Left = 286,
            Top = 198,
            Width = 78,
            Height = 30
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 372,
            Top = 198,
            Width = 78,
            Height = 30
        };

        Controls.AddRange(new Control[]
        {
            heading,
            _mergeRadioButton,
            mergeDescription,
            _replaceRadioButton,
            replaceDescription,
            importButton,
            cancelButton
        });

        AcceptButton = importButton;
        CancelButton = cancelButton;
    }
}

internal enum ActivityRuleCategory
{
    File,
    User,
    App
}

public sealed class RuleSettingsExportPackage
{
    public int SchemaVersion { get; set; } = 2;
    public string DeskPulseVersion { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public List<ActivityRuleSetting>? FileActivity { get; set; }
    public List<ActivityRuleSetting>? FolderActivity { get; set; }
    public List<ActivityRuleSetting>? AppActivity { get; set; }
}

internal sealed class ActivityRuleEditor : UserControl
{
    private readonly ActivityRuleCategory _category;
    private readonly DataGridView _grid = new();
    private readonly TextBox _valueText = new();
    private List<ActivityRuleSetting> _userRules = new();
    private IReadOnlyList<ActivityRuleSetting> _windowsDefaultRules = Array.Empty<ActivityRuleSetting>();
    private bool _windowsDefaultsActive;

    public ActivityRuleEditor(ActivityRuleCategory category)
    {
        _category = category;
        BuildUi();
    }

    private void BuildUi()
    {
        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = _category == ActivityRuleCategory.File ? 34 : 38,
            Text = _category switch
            {
                ActivityRuleCategory.File => "Add file, extension, and folder-path patterns. Use \\* for one folder or \\**\\* for that folder and all subfolders.",
                ActivityRuleCategory.User => "Select which predefined user/session events DeskPulse may log, such as lock, unlock, logon and logoff.",
                _ => "Rules for application start and close activity. Rules are checked from top to bottom; the first match wins."
            },
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        Panel? addPanel = _category == ActivityRuleCategory.User
            ? null
            : new Panel { Dock = DockStyle.Top, Height = 42 };
        var valueLabel = new Label
        {
            Text = _category switch
            {
                ActivityRuleCategory.File => "File / pattern",
                ActivityRuleCategory.User => "Event / text",
                ActivityRuleCategory.App => "App / pattern",
                _ => "Value"
            },
            Left = 0,
            Top = 9,
            Width = _category is ActivityRuleCategory.File or ActivityRuleCategory.App ? 92 : 78
        };

        if (_category is ActivityRuleCategory.File or ActivityRuleCategory.App)
        {
            _valueText.Left = 98;
            _valueText.Top = 5;
            _valueText.Width = _category == ActivityRuleCategory.App ? 335 : 390;

            var browse = new Button
            {
                Text = "Browse...",
                Left = _category == ActivityRuleCategory.App ? 440 : 495,
                Top = 4,
                Width = 82,
                Height = 29,
                FlatStyle = FlatStyle.System
            };
            browse.Click += (_, _) =>
            {
                if (_category == ActivityRuleCategory.App)
                    BrowseForApplication();
                else
                    BrowseForExactFile();
            };

            var add = new Button
            {
                Text = "Add",
                Left = _category == ActivityRuleCategory.App ? 700 : 688,
                Top = 4,
                Width = _category == ActivityRuleCategory.App ? 70 : 98,
                Height = 29,
                FlatStyle = FlatStyle.System
            };
            add.Click += (_, _) => AddRule();

            addPanel!.Controls.AddRange(new Control[] { valueLabel, _valueText, browse });

            if (_category == ActivityRuleCategory.File)
            {
                var addFolder = new Button
                {
                    Text = "Add Folder...",
                    Left = 583,
                    Top = 4,
                    Width = 98,
                    Height = 29,
                    FlatStyle = FlatStyle.System
                };
                addFolder.Click += (_, _) => AddFolderPattern();
                addPanel.Controls.Add(addFolder);
            }

            if (_category == ActivityRuleCategory.App)
            {
                var addApp = new Button
                {
                    Text = "Add App...",
                    Left = 529,
                    Top = 4,
                    Width = 164,
                    Height = 29,
                    FlatStyle = FlatStyle.System
                };
                addApp.Click += (_, _) => AddInstalledApplications();
                addPanel!.Controls.Add(addApp);
            }

            addPanel!.Controls.Add(add);
        }
        // User Activity uses the predefined event list only. Its tab intentionally has no manual rule-entry controls.

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = _category != ActivityRuleCategory.User;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.BackgroundColor = System.Drawing.SystemColors.Window;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled",
            HeaderText = _category == ActivityRuleCategory.User ? "Log" : "On",
            Width = _category == ActivityRuleCategory.User ? 42 : 34
        });
        if (_category != ActivityRuleCategory.User)
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", Width = 108, ReadOnly = true });

        _grid.CellBeginEdit += (_, e) => { if (e.RowIndex >= 0 && Equals(_grid.Rows[e.RowIndex].Tag, "WindowsDefault")) e.Cancel = true; };

        if (_category == ActivityRuleCategory.File)
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Exclude", HeaderText = "Exclude", Width = 58 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Include", Width = 58 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "File / extension / wildcard pattern",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 300
            });
            _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var row = _grid.Rows[e.RowIndex];
                var name = _grid.Columns[e.ColumnIndex].Name;
                if (name == "Exclude" && Checked(row, "Exclude")) row.Cells["Include"].Value = false;
                if (name == "Include" && Checked(row, "Include")) row.Cells["Exclude"].Value = false;
                if (!Checked(row, "Exclude") && !Checked(row, "Include")) row.Cells["Include"].Value = true;
            };
        }
        else if (_category == ActivityRuleCategory.App)
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Exclude", HeaderText = "Exclude", Width = 58 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Include", Width = 58 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Application / process / executable pattern", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 300 });
            _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var row = _grid.Rows[e.RowIndex];
                var name = _grid.Columns[e.ColumnIndex].Name;
                if (name == "Exclude" && Checked(row, "Exclude")) row.Cells["Include"].Value = false;
                if (name == "Include" && Checked(row, "Include")) row.Cells["Exclude"].Value = false;
                if (!Checked(row, "Exclude") && !Checked(row, "Include")) row.Cells["Include"].Value = true;
            };
        }
        else
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "User / session event",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 300,
                ReadOnly = true
            });
            _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight };
        var actionNames = _category == ActivityRuleCategory.User
            ? new[] { "Reset Defaults" }
            : new[] { "Move Up", "Move Down", "Remove", "Duplicate", "Reset Defaults" };

        foreach (var spec in actionNames)
        {
            var button = new Button { Text = spec, Width = spec == "Reset Defaults" ? 112 : 92, Height = 29, FlatStyle = FlatStyle.System };
            button.Click += (_, _) => HandleAction(spec);
            buttons.Controls.Add(button);
        }

        Controls.Add(_grid);
        Controls.Add(buttons);

        if (_category == ActivityRuleCategory.File)
        {
            Controls.Add(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Text = "Files, extensions, and folder patterns not matched by an Include rule are not monitored. Use \\* for one folder and \\**\\* for all subfolders. Explicit App Activity rules take precedence for matching executable files.",
                ForeColor = System.Drawing.SystemColors.GrayText,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            });
        }

        if (addPanel != null)
            Controls.Add(addPanel);

        Controls.Add(intro);
    }

    private void AddFolderPattern()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to add as a File Activity pattern",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var recursive = MessageBox.Show(
            this,
            "Include files in all subfolders too?\n\nYes: folder and all subfolders\nNo: selected folder only",
            "Add Folder Pattern",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (recursive == DialogResult.Cancel)
            return;

        var folder = dialog.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pattern = folder + (recursive == DialogResult.Yes ? @"\**\*" : @"\*");
        _grid.Rows.Add(true, "User", false, true, pattern);
    }

    private void BrowseForExactFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a file to add to File Activity rules",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _valueText.Text = dialog.FileName;
    }

    private void BrowseForApplication()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select an application executable to add to App Activity rules",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _valueText.Text = dialog.FileName;
    }

    private void AddInstalledApplications()
    {
        using var dialog = new InstalledAppSelectionForm();

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        static string ExecutableKey(string value)
        {
            var trimmed = (value ?? "").Trim().Trim('"');
            var fileName = Path.GetFileName(trimmed);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = trimmed;
            return Path.GetFileNameWithoutExtension(fileName).Trim();
        }

        var existingExecutableKeys = _grid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => ExecutableKey(Convert.ToString(row.Cells["Value"].Value) ?? ""))
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var executablePath in dialog.SelectedExecutablePaths)
        {
            var value = executablePath.Trim();
            var key = ExecutableKey(value);
            if (value.Length == 0 || key.Length == 0 || !existingExecutableKeys.Add(key))
                continue;

            // Installed applications are appended as enabled Include rules.
            _grid.Rows.Add(true, "User", false, true, value);
        }
    }

    private void AddRule()
    {
        var value = _valueText.Text.Trim();
        if (value.Length == 0) return;

        if (_category == ActivityRuleCategory.File)
        {
            // New File Activity rules are enabled Include rules and are appended at the bottom.
            _grid.Rows.Add(true, "User", false, true, value);
        }
        else if (_category == ActivityRuleCategory.App)
        {
            // New App Activity rules are enabled Include rules and are appended at the bottom.
            _grid.Rows.Add(true, "User", false, true, value);
        }
        else
        {
            // User Activity rules are managed through the predefined defaults only.
            return;
        }

        _valueText.Clear();
    }

    public void LoadRuleSettings(IEnumerable<ActivityRuleSetting> rules)
    {
        _userRules = (rules ?? Array.Empty<ActivityRuleSetting>()).Select(r => r.Clone()).ToList();
        RenderRules();
    }

    public void SetWindowsDefaultRules(IEnumerable<ActivityRuleSetting> rules, bool active)
    {
        if (_grid.Rows.Count > 0) _userRules = GetRuleSettings();
        _windowsDefaultRules = (rules ?? Array.Empty<ActivityRuleSetting>()).Select(r => r.Clone()).ToList();
        _windowsDefaultsActive = active;
        RenderRules();
    }

    private void RenderRules()
    {
        _grid.Rows.Clear();
        if (_category != ActivityRuleCategory.User)
        {
            foreach (var setting in _windowsDefaultRules)
            {
                var include = setting.Action.Equals("Include", StringComparison.OrdinalIgnoreCase);
                var index = _grid.Rows.Add(_windowsDefaultsActive, "Windows default", !include, include, setting.Value);
                var row = _grid.Rows[index];
                row.Tag = "WindowsDefault";
                row.ReadOnly = true;
                row.DefaultCellStyle.BackColor = System.Drawing.SystemColors.Control;
                row.DefaultCellStyle.ForeColor = System.Drawing.SystemColors.GrayText;
                row.Cells["Enabled"].ToolTipText = _windowsDefaultsActive ? "Active built-in exclusion. Controlled by Track Windows system activity." : "Inactive because Track Windows system activity is enabled.";
            }
        }

        foreach (var setting in _userRules)
        {
            if (string.IsNullOrWhiteSpace(setting.Value))
                continue;

            var expectedType = _category switch
            {
                ActivityRuleCategory.File => "file",
                ActivityRuleCategory.User => "event",
                _ => "process"
            };

            if (!setting.RuleType.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_category == ActivityRuleCategory.File)
            {
                var fileIsInclude = setting.Action.Equals("Include", StringComparison.OrdinalIgnoreCase);
                _grid.Rows.Add(setting.Enabled, "User", !fileIsInclude, fileIsInclude, setting.Value);
                continue;
            }

            if (_category == ActivityRuleCategory.App)
            {
                var appIsInclude = setting.Action.Equals("Include", StringComparison.OrdinalIgnoreCase);
                _grid.Rows.Add(setting.Enabled, "User", !appIsInclude, appIsInclude, setting.Value);
                continue;
            }

            _grid.Rows.Add(setting.Enabled, setting.Value);
        }
    }

    public List<ActivityRuleSetting> GetRuleSettings()
    {
        var result = new List<ActivityRuleSetting>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (Equals(row.Tag, "WindowsDefault")) continue;
            var value = row.Cells["Value"].Value?.ToString()?.Trim() ?? "";
            if (value.Length == 0)
                continue;

            var type = _category switch
            {
                ActivityRuleCategory.File => "file",
                ActivityRuleCategory.User => "event",
                _ => "process"
            };

            result.Add(new ActivityRuleSetting
            {
                Enabled = Checked(row, "Enabled"),
                RuleType = type,
                Action = _category == ActivityRuleCategory.User
                    ? "Include"
                    : (Checked(row, "Include") ? "Include" : "Exclude"),
                Value = value,
                IncludeSubfolders = false
            });
        }
        return result;
    }

    public void LoadRules(IEnumerable<string> rules)
    {
        LoadRuleSettings((rules ?? Array.Empty<string>())
            .Select(ActivityRuleSetting.FromRuleText)
            .Where(rule => rule != null)
            .Select(rule => rule!));
    }

    public List<string> GetRules()
    {
        return GetRuleSettings()
            .Where(rule => rule.Enabled)
            .Select(rule => rule.ToRuleText())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static bool Checked(DataGridViewRow row, string name)
    {
        return row.DataGridView?.Columns.Contains(name) == true && row.Cells[name].Value is bool value && value;
    }

    private void HandleAction(string action)
    {
        var row = _grid.CurrentRow;
        if (action == "Reset Defaults")
        {
            LoadRules(_category switch
            {
                ActivityRuleCategory.File => AppSettings.GetDefaultFileActivityRules(),
                ActivityRuleCategory.App => AppSettings.GetDefaultAppActivityRules(),
                ActivityRuleCategory.User => AppSettings.GetDefaultUserActivityRules(),
                _ => Array.Empty<string>()
            });
            return;
        }
        if (row == null || Equals(row.Tag, "WindowsDefault")) return;
        var index = row.Index;
        if (action == "Remove") { _grid.Rows.RemoveAt(index); return; }
        if (action == "Duplicate")
        {
            var values = row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value).ToArray();
            _grid.Rows.Insert(index + 1, values);
            _grid.CurrentCell = _grid.Rows[index + 1].Cells[0];
            return;
        }
        var target = action == "Move Up" ? index - 1 : index + 1;
        if (target < 0 || target >= _grid.Rows.Count) return;
        _grid.Rows.RemoveAt(index);
        _grid.Rows.Insert(target, row);
        _grid.CurrentCell = _grid.Rows[target].Cells[0];
    }
}

