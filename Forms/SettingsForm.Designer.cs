#nullable enable

using System.ComponentModel;
using System.Windows.Forms;

namespace DeskPulse;

partial class SettingsForm
{
    private IContainer? components = null;
    private TabControl _settingsTabControl = null!;
    private TabPage _generalTabPage = null!;
    private TabPage _filesTabPage = null!;
    private TabPage _exportOptionsTabPage = null!;
    private TabPage _maintenanceTabPage = null!;
    private Label _footerLine = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private ToolTip _buttonToolTip = null!;

    private GroupBox _startupGroupBox = null!;
    private CheckBox _startWithWindowsCheckBox = null!;
    private Label _startupHintLabel = null!;
    private Label _quietStartupLabel = null!;
    private GroupBox _behaviourGroupBox = null!;
    private CheckBox _logProgramActivityCheckBox = null!;
    private Label _programActivityHintLabel = null!;
    private Label _behaviourHintLabel = null!;

    private GroupBox _storageGroupBox = null!;
    private Label _dataFolderLabel = null!;
    private TextBox _dataFolderTextBox = null!;
    private Button _browseDataFolderButton = null!;
    private Label _databaseHintLabel = null!;
    private GroupBox _fileFilterGroupBox = null!;
    private CheckBox _ignoreTempFoldersCheckBox = null!;
    private Label _tempFolderHintLabel = null!;
    private GroupBox _fileTypesGroupBox = null!;
    private Label _availableExtensionsLabel = null!;
    private ListBox _availableExtensionsListBox = null!;
    private Button _addExtensionButton = null!;
    private Button _removeExtensionButton = null!;
    private Label _monitoredExtensionsLabel = null!;
    private ListBox _monitoredExtensionsListBox = null!;
    private Label _dragHintLabel = null!;

    private Label _exportIntroLabel = null!;
    private GroupBox _worksheetsGroupBox = null!;
    private CheckedListBox _exportSheetsCheckedListBox = null!;
    private Button _moveSheetUpButton = null!;
    private Button _moveSheetDownButton = null!;
    private Button _resetExportSheetsButton = null!;
    private Label _worksheetHintLabel = null!;
    private GroupBox _worksheetColumnsGroupBox = null!;
    private TabControl _exportFieldTabControl = null!;
    private Button _moveFieldUpButton = null!;
    private Button _moveFieldDownButton = null!;
    private Button _selectAllFieldsButton = null!;
    private Button _clearFieldsButton = null!;
    private Label _fieldHintLabel = null!;

    private Label _maintenanceIntroLabel = null!;
    private TabControl _maintenanceSubTabControl = null!;
    private TabPage _maintenanceDatabaseTabPage = null!;
    private TabPage _maintenanceStatisticsTabPage = null!;
    private TabPage _maintenanceCleanupTabPage = null!;
    private TabPage _maintenanceLoggingRulesTabPage = null!;
    private TabPage _maintenanceDiagnosticsTabPage = null!;
    private GroupBox _maintenanceDatabaseGroupBox = null!;
    private TextBox _maintenanceDatabaseInfoTextBox = null!;
    private Button _maintenanceRefreshDatabaseButton = null!;
    private Button _maintenanceOpenDataFolderButton = null!;
    private Button _maintenanceOpenProgramFolderButton = null!;
    private GroupBox _maintenanceDatabaseNotesGroupBox = null!;
    private Label _maintenanceDatabaseNotesLabel = null!;
    private Label _maintenanceStatisticsTopLabel = null!;
    private Button _maintenanceStatisticsRefreshButton = null!;
    private Button _maintenanceAddFileExclusionButton = null!;
    private Button _maintenanceAddFolderExclusionButton = null!;
    private Button _maintenanceAddProcessExclusionButton = null!;
    private GroupBox _maintenanceGeneratedFilesGroupBox = null!;
    private Button _maintenanceDeleteExportButton = null!;
    private Button _maintenanceDeleteDiagnosticsButton = null!;
    private Button _maintenanceDeleteStartupLogButton = null!;
    private Label _maintenanceGeneratedFilesHintLabel = null!;
    private GroupBox _maintenanceUnwantedDataGroupBox = null!;
    private Button _maintenanceRemoveUnwantedDataButton = null!;
    private Label _maintenanceUnwantedDataHintLabel = null!;
    private GroupBox _maintenanceDatabaseCleanupGroupBox = null!;
    private Button _maintenanceDeleteFileActivityButton = null!;
    private Button _maintenanceDeleteUserActivityButton = null!;
    private Button _maintenanceDeleteProgramActivityButton = null!;
    private Button _maintenanceDeleteAllActivityButton = null!;
    private Label _maintenanceDatabaseCleanupHintLabel = null!;
    private GroupBox _maintenanceAddRuleGroupBox = null!;
    private Button _maintenanceBrowseRuleButton = null!;
    private Button _maintenanceAddRuleButton = null!;
    private Label _maintenanceAddRuleHintLabel = null!;
    private GroupBox _maintenanceRulesGroupBox = null!;
    private Button _maintenanceMoveRuleUpButton = null!;
    private Button _maintenanceMoveRuleDownButton = null!;
    private Button _maintenanceRemoveRuleButton = null!;
    private Button _maintenanceDuplicateRuleButton = null!;
    private Button _maintenanceResetRulesButton = null!;
    private Label _maintenanceRulesHintLabel = null!;
    private GroupBox _maintenancePastRecordsGroupBox = null!;
    private Button _maintenanceRemovePastRecordsButton = null!;
    private Label _maintenancePastRecordsHintLabel = null!;
    private GroupBox _maintenanceDiagnosticsGroupBox = null!;
    private Label _maintenanceDiagnosticsStatusLabel = null!;
    private Button _maintenanceOpenDiagnosticsLogButton = null!;
    private Button _maintenanceShowActiveExtensionsButton = null!;
    private Button _maintenanceShowStartupStatusButton = null!;
    private GroupBox _maintenanceRegistryGroupBox = null!;
    private Label _maintenanceRegistryPathLabel = null!;
    private Button _maintenanceRemoveRegistrySettingsButton = null!;
    private Label _maintenanceRegistryHintLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _buttonToolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new Container();
        _buttonToolTip = new ToolTip(components)
        {
            AutoPopDelay = 15000,
            InitialDelay = 450,
            ReshowDelay = 100,
            ShowAlways = true
        };

        _settingsTabControl = new TabControl();
        _generalTabPage = new TabPage();
        _filesTabPage = new TabPage();
        _exportOptionsTabPage = new TabPage();
        _maintenanceTabPage = new TabPage();
        _footerLine = new Label();
        _saveButton = new Button();
        _cancelButton = new Button();

        _startupGroupBox = new GroupBox();
        _startWithWindowsCheckBox = new CheckBox();
        _startupHintLabel = new Label();
        _quietStartupLabel = new Label();
        _behaviourGroupBox = new GroupBox();
        _logProgramActivityCheckBox = new CheckBox();
        _programActivityHintLabel = new Label();
        _behaviourHintLabel = new Label();

        _storageGroupBox = new GroupBox();
        _dataFolderLabel = new Label();
        _dataFolderTextBox = new TextBox();
        _browseDataFolderButton = new Button();
        _databaseHintLabel = new Label();
        _fileFilterGroupBox = new GroupBox();
        _ignoreTempFoldersCheckBox = new CheckBox();
        _tempFolderHintLabel = new Label();
        _fileTypesGroupBox = new GroupBox();
        _availableExtensionsLabel = new Label();
        _availableExtensionsListBox = new ListBox();
        _addExtensionButton = new Button();
        _removeExtensionButton = new Button();
        _monitoredExtensionsLabel = new Label();
        _monitoredExtensionsListBox = new ListBox();
        _dragHintLabel = new Label();

        _exportIntroLabel = new Label();
        _worksheetsGroupBox = new GroupBox();
        _exportSheetsCheckedListBox = new CheckedListBox();
        _moveSheetUpButton = new Button();
        _moveSheetDownButton = new Button();
        _resetExportSheetsButton = new Button();
        _worksheetHintLabel = new Label();
        _worksheetColumnsGroupBox = new GroupBox();
        _exportFieldTabControl = new TabControl();
        _moveFieldUpButton = new Button();
        _moveFieldDownButton = new Button();
        _selectAllFieldsButton = new Button();
        _clearFieldsButton = new Button();
        _fieldHintLabel = new Label();

        _maintenanceIntroLabel = new Label();
        _maintenanceSubTabControl = new TabControl();
        _maintenanceDatabaseTabPage = new TabPage();
        _maintenanceStatisticsTabPage = new TabPage();
        _maintenanceCleanupTabPage = new TabPage();
        _maintenanceLoggingRulesTabPage = new TabPage();
        _maintenanceDiagnosticsTabPage = new TabPage();
        _maintenanceDatabaseGroupBox = new GroupBox();
        _maintenanceDatabaseInfoTextBox = new TextBox();
        _maintenanceRefreshDatabaseButton = new Button();
        _maintenanceOpenDataFolderButton = new Button();
        _maintenanceOpenProgramFolderButton = new Button();
        _maintenanceDatabaseNotesGroupBox = new GroupBox();
        _maintenanceDatabaseNotesLabel = new Label();
        _maintenanceStatisticsTopLabel = new Label();
        _statisticsViewComboBox = new ComboBox();
        _maintenanceStatisticsRefreshButton = new Button();
        _maintenanceAddFileExclusionButton = new Button();
        _maintenanceAddFolderExclusionButton = new Button();
        _maintenanceAddProcessExclusionButton = new Button();
        _statisticsGrid = new DataGridView();
        _maintenanceGeneratedFilesGroupBox = new GroupBox();
        _maintenanceDeleteExportButton = new Button();
        _maintenanceDeleteDiagnosticsButton = new Button();
        _maintenanceDeleteStartupLogButton = new Button();
        _maintenanceGeneratedFilesHintLabel = new Label();
        _maintenanceUnwantedDataGroupBox = new GroupBox();
        _maintenanceRemoveUnwantedDataButton = new Button();
        _maintenanceUnwantedDataHintLabel = new Label();
        _maintenanceDatabaseCleanupGroupBox = new GroupBox();
        _maintenanceDeleteFileActivityButton = new Button();
        _maintenanceDeleteUserActivityButton = new Button();
        _maintenanceDeleteProgramActivityButton = new Button();
        _maintenanceDeleteAllActivityButton = new Button();
        _maintenanceDatabaseCleanupHintLabel = new Label();
        _maintenanceAddRuleGroupBox = new GroupBox();
        _manualRuleFolderRadioButton = new RadioButton();
        _manualRuleFileRadioButton = new RadioButton();
        _manualRuleProcessRadioButton = new RadioButton();
        _manualRuleExcludeRadioButton = new RadioButton();
        _manualRuleIncludeRadioButton = new RadioButton();
        _manualRuleSubfoldersCheckBox = new CheckBox();
        _manualLoggingRuleTextBox = new TextBox();
        _maintenanceBrowseRuleButton = new Button();
        _maintenanceAddRuleButton = new Button();
        _maintenanceAddRuleHintLabel = new Label();
        _maintenanceRulesGroupBox = new GroupBox();
        _loggingRulesGridView = new DataGridView();
        _maintenanceMoveRuleUpButton = new Button();
        _maintenanceMoveRuleDownButton = new Button();
        _maintenanceRemoveRuleButton = new Button();
        _maintenanceDuplicateRuleButton = new Button();
        _maintenanceResetRulesButton = new Button();
        _maintenanceRulesHintLabel = new Label();
        _maintenancePastRecordsGroupBox = new GroupBox();
        _maintenanceRemovePastRecordsButton = new Button();
        _maintenancePastRecordsHintLabel = new Label();
        _maintenanceDiagnosticsGroupBox = new GroupBox();
        _maintenanceDiagnosticsStatusLabel = new Label();
        _maintenanceOpenDiagnosticsLogButton = new Button();
        _maintenanceShowActiveExtensionsButton = new Button();
        _maintenanceShowStartupStatusButton = new Button();
        _maintenanceRegistryGroupBox = new GroupBox();
        _maintenanceRegistryPathLabel = new Label();
        _maintenanceRemoveRegistrySettingsButton = new Button();
        _maintenanceRegistryHintLabel = new Label();

        _settingsTabControl.SuspendLayout();
        _generalTabPage.SuspendLayout();
        _filesTabPage.SuspendLayout();
        _exportOptionsTabPage.SuspendLayout();
        _startupGroupBox.SuspendLayout();
        _behaviourGroupBox.SuspendLayout();
        _storageGroupBox.SuspendLayout();
        _fileFilterGroupBox.SuspendLayout();
        _fileTypesGroupBox.SuspendLayout();
        _worksheetsGroupBox.SuspendLayout();
        _worksheetColumnsGroupBox.SuspendLayout();
        _maintenanceTabPage.SuspendLayout();
        _maintenanceSubTabControl.SuspendLayout();
        _maintenanceDatabaseTabPage.SuspendLayout();
        _maintenanceStatisticsTabPage.SuspendLayout();
        _maintenanceCleanupTabPage.SuspendLayout();
        _maintenanceLoggingRulesTabPage.SuspendLayout();
        _maintenanceDiagnosticsTabPage.SuspendLayout();
        _maintenanceDatabaseGroupBox.SuspendLayout();
        _maintenanceDatabaseNotesGroupBox.SuspendLayout();
        ((ISupportInitialize)_statisticsGrid).BeginInit();
        _maintenanceGeneratedFilesGroupBox.SuspendLayout();
        _maintenanceUnwantedDataGroupBox.SuspendLayout();
        _maintenanceDatabaseCleanupGroupBox.SuspendLayout();
        _maintenanceAddRuleGroupBox.SuspendLayout();
        _maintenanceRulesGroupBox.SuspendLayout();
        ((ISupportInitialize)_loggingRulesGridView).BeginInit();
        _maintenancePastRecordsGroupBox.SuspendLayout();
        _maintenanceDiagnosticsGroupBox.SuspendLayout();
        _maintenanceRegistryGroupBox.SuspendLayout();
        SuspendLayout();

        // _settingsTabControl
        _settingsTabControl.Controls.Add(_generalTabPage);
        _settingsTabControl.Controls.Add(_filesTabPage);
        _settingsTabControl.Controls.Add(_exportOptionsTabPage);
        _settingsTabControl.Controls.Add(_maintenanceTabPage);
        _settingsTabControl.Location = new System.Drawing.Point(16, 16);
        _settingsTabControl.Name = "_settingsTabControl";
        _settingsTabControl.SelectedIndex = 0;
        _settingsTabControl.Size = new System.Drawing.Size(888, 600);
        _settingsTabControl.TabIndex = 0;

        // _generalTabPage
        _generalTabPage.BackColor = System.Drawing.SystemColors.Window;
        _generalTabPage.Controls.Add(_startupGroupBox);
        _generalTabPage.Controls.Add(_behaviourGroupBox);
        _generalTabPage.Location = new System.Drawing.Point(4, 24);
        _generalTabPage.Name = "_generalTabPage";
        _generalTabPage.Padding = new Padding(16);
        _generalTabPage.Size = new System.Drawing.Size(880, 572);
        _generalTabPage.TabIndex = 0;
        _generalTabPage.Text = "General";

        // _startupGroupBox
        _startupGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _startupGroupBox.Controls.Add(_startWithWindowsCheckBox);
        _startupGroupBox.Controls.Add(_startupHintLabel);
        _startupGroupBox.Controls.Add(_quietStartupLabel);
        _startupGroupBox.Location = new System.Drawing.Point(24, 24);
        _startupGroupBox.Name = "_startupGroupBox";
        _startupGroupBox.Padding = new Padding(12);
        _startupGroupBox.Size = new System.Drawing.Size(820, 150);
        _startupGroupBox.TabIndex = 0;
        _startupGroupBox.TabStop = false;
        _startupGroupBox.Text = "Windows startup";

        // _startWithWindowsCheckBox
        _startWithWindowsCheckBox.Location = new System.Drawing.Point(18, 32);
        _startWithWindowsCheckBox.Name = "_startWithWindowsCheckBox";
        _startWithWindowsCheckBox.Size = new System.Drawing.Size(520, 22);
        _startWithWindowsCheckBox.TabIndex = 0;
        _startWithWindowsCheckBox.Text = "Start DeskPulse when I log in to Windows";
        _startWithWindowsCheckBox.UseVisualStyleBackColor = true;

        // _startupHintLabel
        _startupHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _startupHintLabel.Location = new System.Drawing.Point(40, 62);
        _startupHintLabel.Name = "_startupHintLabel";
        _startupHintLabel.Size = new System.Drawing.Size(740, 22);
        _startupHintLabel.TabIndex = 1;
        _startupHintLabel.Text = "Creates a current-user Windows Task Scheduler entry and starts DeskPulse with highest privileges when you log in.";

        // _quietStartupLabel
        _quietStartupLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _quietStartupLabel.Location = new System.Drawing.Point(40, 88);
        _quietStartupLabel.Name = "_quietStartupLabel";
        _quietStartupLabel.Size = new System.Drawing.Size(740, 22);
        _quietStartupLabel.TabIndex = 2;
        _quietStartupLabel.Text = "DeskPulse starts quietly in the tray. Startup errors are still shown if monitoring cannot start.";

        // _behaviourGroupBox
        _behaviourGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _behaviourGroupBox.Controls.Add(_logProgramActivityCheckBox);
        _behaviourGroupBox.Controls.Add(_programActivityHintLabel);
        _behaviourGroupBox.Controls.Add(_behaviourHintLabel);
        _behaviourGroupBox.Location = new System.Drawing.Point(24, 196);
        _behaviourGroupBox.Name = "_behaviourGroupBox";
        _behaviourGroupBox.Padding = new Padding(12);
        _behaviourGroupBox.Size = new System.Drawing.Size(820, 150);
        _behaviourGroupBox.TabIndex = 1;
        _behaviourGroupBox.TabStop = false;
        _behaviourGroupBox.Text = "Application behaviour";

        // _logProgramActivityCheckBox
        _logProgramActivityCheckBox.Location = new System.Drawing.Point(18, 32);
        _logProgramActivityCheckBox.Name = "_logProgramActivityCheckBox";
        _logProgramActivityCheckBox.Size = new System.Drawing.Size(520, 22);
        _logProgramActivityCheckBox.TabIndex = 0;
        _logProgramActivityCheckBox.Text = "Log program start and close activity";
        _logProgramActivityCheckBox.UseVisualStyleBackColor = true;

        // _programActivityHintLabel
        _programActivityHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _programActivityHintLabel.Location = new System.Drawing.Point(40, 62);
        _programActivityHintLabel.Name = "_programActivityHintLabel";
        _programActivityHintLabel.Size = new System.Drawing.Size(740, 36);
        _programActivityHintLabel.TabIndex = 1;
        _programActivityHintLabel.Text = "Records programs that start and close in the current interactive Windows session. This is not a full system-service audit log.";

        // _behaviourHintLabel
        _behaviourHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _behaviourHintLabel.Location = new System.Drawing.Point(18, 106);
        _behaviourHintLabel.Name = "_behaviourHintLabel";
        _behaviourHintLabel.Size = new System.Drawing.Size(760, 22);
        _behaviourHintLabel.TabIndex = 2;
        _behaviourHintLabel.Text = "DeskPulse keeps running from the tray icon. Left-click opens Export/Settings; right-click opens About/Exit.";

        // _filesTabPage
        _filesTabPage.BackColor = System.Drawing.SystemColors.Window;
        _filesTabPage.Controls.Add(_storageGroupBox);
        _filesTabPage.Controls.Add(_fileFilterGroupBox);
        _filesTabPage.Controls.Add(_fileTypesGroupBox);
        _filesTabPage.Location = new System.Drawing.Point(4, 24);
        _filesTabPage.Name = "_filesTabPage";
        _filesTabPage.Padding = new Padding(16);
        _filesTabPage.Size = new System.Drawing.Size(880, 572);
        _filesTabPage.TabIndex = 1;
        _filesTabPage.Text = "Files";

        // _storageGroupBox
        _storageGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _storageGroupBox.Controls.Add(_dataFolderLabel);
        _storageGroupBox.Controls.Add(_dataFolderTextBox);
        _storageGroupBox.Controls.Add(_browseDataFolderButton);
        _storageGroupBox.Controls.Add(_databaseHintLabel);
        _storageGroupBox.Location = new System.Drawing.Point(24, 24);
        _storageGroupBox.Name = "_storageGroupBox";
        _storageGroupBox.Padding = new Padding(12);
        _storageGroupBox.Size = new System.Drawing.Size(820, 122);
        _storageGroupBox.TabIndex = 0;
        _storageGroupBox.TabStop = false;
        _storageGroupBox.Text = "Storage";

        // _dataFolderLabel
        _dataFolderLabel.Location = new System.Drawing.Point(18, 34);
        _dataFolderLabel.Name = "_dataFolderLabel";
        _dataFolderLabel.Size = new System.Drawing.Size(90, 22);
        _dataFolderLabel.TabIndex = 0;
        _dataFolderLabel.Text = "Data folder";

        // _dataFolderTextBox
        _dataFolderTextBox.Location = new System.Drawing.Point(112, 30);
        _dataFolderTextBox.Name = "_dataFolderTextBox";
        _dataFolderTextBox.Size = new System.Drawing.Size(560, 23);
        _dataFolderTextBox.TabIndex = 1;

        // _browseDataFolderButton
        _browseDataFolderButton.FlatStyle = FlatStyle.System;
        _browseDataFolderButton.Location = new System.Drawing.Point(686, 28);
        _browseDataFolderButton.Name = "_browseDataFolderButton";
        _browseDataFolderButton.Size = new System.Drawing.Size(100, 30);
        _browseDataFolderButton.TabIndex = 2;
        _browseDataFolderButton.Text = "Browse...";
        _browseDataFolderButton.UseVisualStyleBackColor = true;
        _browseDataFolderButton.Click += BrowseDataFolderButton_Click;

        // _databaseHintLabel
        _databaseHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _databaseHintLabel.Location = new System.Drawing.Point(112, 62);
        _databaseHintLabel.Name = "_databaseHintLabel";
        _databaseHintLabel.Size = new System.Drawing.Size(650, 22);
        _databaseHintLabel.TabIndex = 3;
        _databaseHintLabel.Text = "Live data: DeskPulse.db    Export report: DeskPulse-export.xlsx";

        // _fileFilterGroupBox
        _fileFilterGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _fileFilterGroupBox.Controls.Add(_ignoreTempFoldersCheckBox);
        _fileFilterGroupBox.Controls.Add(_tempFolderHintLabel);
        _fileFilterGroupBox.Location = new System.Drawing.Point(24, 166);
        _fileFilterGroupBox.Name = "_fileFilterGroupBox";
        _fileFilterGroupBox.Padding = new Padding(12);
        _fileFilterGroupBox.Size = new System.Drawing.Size(820, 88);
        _fileFilterGroupBox.TabIndex = 1;
        _fileFilterGroupBox.TabStop = false;
        _fileFilterGroupBox.Text = "File activity filters";

        // _ignoreTempFoldersCheckBox
        _ignoreTempFoldersCheckBox.Location = new System.Drawing.Point(18, 30);
        _ignoreTempFoldersCheckBox.Name = "_ignoreTempFoldersCheckBox";
        _ignoreTempFoldersCheckBox.Size = new System.Drawing.Size(360, 22);
        _ignoreTempFoldersCheckBox.TabIndex = 0;
        _ignoreTempFoldersCheckBox.Text = "Ignore temporary-folder activity";
        _ignoreTempFoldersCheckBox.UseVisualStyleBackColor = true;

        // _tempFolderHintLabel
        _tempFolderHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _tempFolderHintLabel.Location = new System.Drawing.Point(40, 56);
        _tempFolderHintLabel.Name = "_tempFolderHintLabel";
        _tempFolderHintLabel.Size = new System.Drawing.Size(720, 22);
        _tempFolderHintLabel.TabIndex = 1;
        _tempFolderHintLabel.Text = "Recommended. Turn off only when you deliberately want to include Windows temp-folder activity.";

        // _fileTypesGroupBox
        _fileTypesGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _fileTypesGroupBox.Controls.Add(_availableExtensionsLabel);
        _fileTypesGroupBox.Controls.Add(_availableExtensionsListBox);
        _fileTypesGroupBox.Controls.Add(_addExtensionButton);
        _fileTypesGroupBox.Controls.Add(_removeExtensionButton);
        _fileTypesGroupBox.Controls.Add(_monitoredExtensionsLabel);
        _fileTypesGroupBox.Controls.Add(_monitoredExtensionsListBox);
        _fileTypesGroupBox.Controls.Add(_dragHintLabel);
        _fileTypesGroupBox.Location = new System.Drawing.Point(24, 274);
        _fileTypesGroupBox.Name = "_fileTypesGroupBox";
        _fileTypesGroupBox.Padding = new Padding(12);
        _fileTypesGroupBox.Size = new System.Drawing.Size(820, 280);
        _fileTypesGroupBox.TabIndex = 2;
        _fileTypesGroupBox.TabStop = false;
        _fileTypesGroupBox.Text = "Monitored file types";

        // _availableExtensionsLabel
        _availableExtensionsLabel.Location = new System.Drawing.Point(18, 28);
        _availableExtensionsLabel.Name = "_availableExtensionsLabel";
        _availableExtensionsLabel.Size = new System.Drawing.Size(300, 22);
        _availableExtensionsLabel.TabIndex = 0;
        _availableExtensionsLabel.Text = "Available file types";

        // _availableExtensionsListBox
        _availableExtensionsListBox.AllowDrop = true;
        _availableExtensionsListBox.DisplayMember = "DisplayName";
        _availableExtensionsListBox.IntegralHeight = false;
        _availableExtensionsListBox.ItemHeight = 15;
        _availableExtensionsListBox.Location = new System.Drawing.Point(18, 52);
        _availableExtensionsListBox.Name = "_availableExtensionsListBox";
        _availableExtensionsListBox.SelectionMode = SelectionMode.One;
        _availableExtensionsListBox.Size = new System.Drawing.Size(330, 190);
        _availableExtensionsListBox.TabIndex = 1;
        _availableExtensionsListBox.DragDrop += AvailableExtensionsListBox_DragDrop;
        _availableExtensionsListBox.DragEnter += ListBoxDragEnter;
        _availableExtensionsListBox.MouseDown += ListBoxMouseDown;

        // _addExtensionButton
        _addExtensionButton.FlatStyle = FlatStyle.System;
        _addExtensionButton.Location = new System.Drawing.Point(366, 95);
        _addExtensionButton.Name = "_addExtensionButton";
        _addExtensionButton.Size = new System.Drawing.Size(88, 30);
        _addExtensionButton.TabIndex = 2;
        _addExtensionButton.Text = "Add  >";
        _addExtensionButton.UseVisualStyleBackColor = true;
        _addExtensionButton.Click += AddExtensionButton_Click;

        // _removeExtensionButton
        _removeExtensionButton.FlatStyle = FlatStyle.System;
        _removeExtensionButton.Location = new System.Drawing.Point(366, 135);
        _removeExtensionButton.Name = "_removeExtensionButton";
        _removeExtensionButton.Size = new System.Drawing.Size(88, 30);
        _removeExtensionButton.TabIndex = 3;
        _removeExtensionButton.Text = "<  Remove";
        _removeExtensionButton.UseVisualStyleBackColor = true;
        _removeExtensionButton.Click += RemoveExtensionButton_Click;

        // _monitoredExtensionsLabel
        _monitoredExtensionsLabel.Location = new System.Drawing.Point(472, 28);
        _monitoredExtensionsLabel.Name = "_monitoredExtensionsLabel";
        _monitoredExtensionsLabel.Size = new System.Drawing.Size(300, 22);
        _monitoredExtensionsLabel.TabIndex = 4;
        _monitoredExtensionsLabel.Text = "Currently monitored";

        // _monitoredExtensionsListBox
        _monitoredExtensionsListBox.AllowDrop = true;
        _monitoredExtensionsListBox.IntegralHeight = false;
        _monitoredExtensionsListBox.ItemHeight = 15;
        _monitoredExtensionsListBox.Location = new System.Drawing.Point(472, 52);
        _monitoredExtensionsListBox.Name = "_monitoredExtensionsListBox";
        _monitoredExtensionsListBox.SelectionMode = SelectionMode.One;
        _monitoredExtensionsListBox.Size = new System.Drawing.Size(330, 190);
        _monitoredExtensionsListBox.TabIndex = 5;
        _monitoredExtensionsListBox.DragDrop += MonitoredExtensionsListBox_DragDrop;
        _monitoredExtensionsListBox.DragEnter += ListBoxDragEnter;
        _monitoredExtensionsListBox.MouseDown += ListBoxMouseDown;

        // _dragHintLabel
        _dragHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _dragHintLabel.Location = new System.Drawing.Point(18, 246);
        _dragHintLabel.Name = "_dragHintLabel";
        _dragHintLabel.Size = new System.Drawing.Size(760, 22);
        _dragHintLabel.TabIndex = 6;
        _dragHintLabel.Text = "You can also drag file types between the lists.";

        // _exportOptionsTabPage
        _exportOptionsTabPage.BackColor = System.Drawing.SystemColors.Window;
        _exportOptionsTabPage.Controls.Add(_exportIntroLabel);
        _exportOptionsTabPage.Controls.Add(_worksheetsGroupBox);
        _exportOptionsTabPage.Controls.Add(_worksheetColumnsGroupBox);
        _exportOptionsTabPage.Location = new System.Drawing.Point(4, 24);
        _exportOptionsTabPage.Name = "_exportOptionsTabPage";
        _exportOptionsTabPage.Padding = new Padding(16);
        _exportOptionsTabPage.Size = new System.Drawing.Size(880, 572);
        _exportOptionsTabPage.TabIndex = 2;
        _exportOptionsTabPage.Text = "Export Options";

        // _exportIntroLabel
        _exportIntroLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _exportIntroLabel.Location = new System.Drawing.Point(24, 22);
        _exportIntroLabel.Name = "_exportIntroLabel";
        _exportIntroLabel.Size = new System.Drawing.Size(820, 28);
        _exportIntroLabel.TabIndex = 0;
        _exportIntroLabel.Text = "Choose which Excel worksheets DeskPulse creates, then choose the columns and column order for each selected worksheet.";

        // _worksheetsGroupBox
        _worksheetsGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _worksheetsGroupBox.Controls.Add(_exportSheetsCheckedListBox);
        _worksheetsGroupBox.Controls.Add(_moveSheetUpButton);
        _worksheetsGroupBox.Controls.Add(_moveSheetDownButton);
        _worksheetsGroupBox.Controls.Add(_resetExportSheetsButton);
        _worksheetsGroupBox.Controls.Add(_worksheetHintLabel);
        _worksheetsGroupBox.Location = new System.Drawing.Point(24, 62);
        _worksheetsGroupBox.Name = "_worksheetsGroupBox";
        _worksheetsGroupBox.Padding = new Padding(12);
        _worksheetsGroupBox.Size = new System.Drawing.Size(300, 492);
        _worksheetsGroupBox.TabIndex = 1;
        _worksheetsGroupBox.TabStop = false;
        _worksheetsGroupBox.Text = "Worksheets";

        // _exportSheetsCheckedListBox
        _exportSheetsCheckedListBox.CheckOnClick = true;
        _exportSheetsCheckedListBox.DisplayMember = "DisplayName";
        _exportSheetsCheckedListBox.IntegralHeight = false;
        _exportSheetsCheckedListBox.Location = new System.Drawing.Point(18, 30);
        _exportSheetsCheckedListBox.Name = "_exportSheetsCheckedListBox";
        _exportSheetsCheckedListBox.SelectionMode = SelectionMode.One;
        _exportSheetsCheckedListBox.Size = new System.Drawing.Size(262, 350);
        _exportSheetsCheckedListBox.TabIndex = 0;
        _exportSheetsCheckedListBox.ItemCheck += ExportSheetsCheckedListBox_ItemCheck;

        // _moveSheetUpButton
        _moveSheetUpButton.FlatStyle = FlatStyle.System;
        _moveSheetUpButton.Location = new System.Drawing.Point(18, 394);
        _moveSheetUpButton.Name = "_moveSheetUpButton";
        _moveSheetUpButton.Size = new System.Drawing.Size(82, 30);
        _moveSheetUpButton.TabIndex = 1;
        _moveSheetUpButton.Text = "Move Up";
        _moveSheetUpButton.UseVisualStyleBackColor = true;
        _moveSheetUpButton.Click += MoveSheetUpButton_Click;

        // _moveSheetDownButton
        _moveSheetDownButton.FlatStyle = FlatStyle.System;
        _moveSheetDownButton.Location = new System.Drawing.Point(108, 394);
        _moveSheetDownButton.Name = "_moveSheetDownButton";
        _moveSheetDownButton.Size = new System.Drawing.Size(92, 30);
        _moveSheetDownButton.TabIndex = 2;
        _moveSheetDownButton.Text = "Move Down";
        _moveSheetDownButton.UseVisualStyleBackColor = true;
        _moveSheetDownButton.Click += MoveSheetDownButton_Click;

        // _resetExportSheetsButton
        _resetExportSheetsButton.FlatStyle = FlatStyle.System;
        _resetExportSheetsButton.Location = new System.Drawing.Point(208, 394);
        _resetExportSheetsButton.Name = "_resetExportSheetsButton";
        _resetExportSheetsButton.Size = new System.Drawing.Size(72, 30);
        _resetExportSheetsButton.TabIndex = 3;
        _resetExportSheetsButton.Text = "Reset";
        _resetExportSheetsButton.UseVisualStyleBackColor = true;
        _resetExportSheetsButton.Click += ResetExportSheetsButton_Click;

        // _worksheetHintLabel
        _worksheetHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _worksheetHintLabel.Location = new System.Drawing.Point(18, 430);
        _worksheetHintLabel.Name = "_worksheetHintLabel";
        _worksheetHintLabel.Size = new System.Drawing.Size(260, 44);
        _worksheetHintLabel.TabIndex = 4;
        _worksheetHintLabel.Text = "Ticked items become workbook tabs. Their order here becomes the Excel worksheet order.";

        // _worksheetColumnsGroupBox
        _worksheetColumnsGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _worksheetColumnsGroupBox.Controls.Add(_exportFieldTabControl);
        _worksheetColumnsGroupBox.Controls.Add(_moveFieldUpButton);
        _worksheetColumnsGroupBox.Controls.Add(_moveFieldDownButton);
        _worksheetColumnsGroupBox.Controls.Add(_selectAllFieldsButton);
        _worksheetColumnsGroupBox.Controls.Add(_clearFieldsButton);
        _worksheetColumnsGroupBox.Controls.Add(_fieldHintLabel);
        _worksheetColumnsGroupBox.Location = new System.Drawing.Point(344, 62);
        _worksheetColumnsGroupBox.Name = "_worksheetColumnsGroupBox";
        _worksheetColumnsGroupBox.Padding = new Padding(12);
        _worksheetColumnsGroupBox.Size = new System.Drawing.Size(500, 492);
        _worksheetColumnsGroupBox.TabIndex = 2;
        _worksheetColumnsGroupBox.TabStop = false;
        _worksheetColumnsGroupBox.Text = "Worksheet columns";

        // _exportFieldTabControl
        _exportFieldTabControl.Location = new System.Drawing.Point(18, 30);
        _exportFieldTabControl.Name = "_exportFieldTabControl";
        _exportFieldTabControl.SelectedIndex = 0;
        _exportFieldTabControl.Size = new System.Drawing.Size(462, 350);
        _exportFieldTabControl.TabIndex = 0;

        // _moveFieldUpButton
        _moveFieldUpButton.FlatStyle = FlatStyle.System;
        _moveFieldUpButton.Location = new System.Drawing.Point(18, 394);
        _moveFieldUpButton.Name = "_moveFieldUpButton";
        _moveFieldUpButton.Size = new System.Drawing.Size(82, 30);
        _moveFieldUpButton.TabIndex = 1;
        _moveFieldUpButton.Text = "Move Up";
        _moveFieldUpButton.UseVisualStyleBackColor = true;
        _moveFieldUpButton.Click += MoveFieldUpButton_Click;

        // _moveFieldDownButton
        _moveFieldDownButton.FlatStyle = FlatStyle.System;
        _moveFieldDownButton.Location = new System.Drawing.Point(108, 394);
        _moveFieldDownButton.Name = "_moveFieldDownButton";
        _moveFieldDownButton.Size = new System.Drawing.Size(92, 30);
        _moveFieldDownButton.TabIndex = 2;
        _moveFieldDownButton.Text = "Move Down";
        _moveFieldDownButton.UseVisualStyleBackColor = true;
        _moveFieldDownButton.Click += MoveFieldDownButton_Click;

        // _selectAllFieldsButton
        _selectAllFieldsButton.FlatStyle = FlatStyle.System;
        _selectAllFieldsButton.Location = new System.Drawing.Point(216, 394);
        _selectAllFieldsButton.Name = "_selectAllFieldsButton";
        _selectAllFieldsButton.Size = new System.Drawing.Size(86, 30);
        _selectAllFieldsButton.TabIndex = 3;
        _selectAllFieldsButton.Text = "Select All";
        _selectAllFieldsButton.UseVisualStyleBackColor = true;
        _selectAllFieldsButton.Click += SelectAllFieldsButton_Click;

        // _clearFieldsButton
        _clearFieldsButton.FlatStyle = FlatStyle.System;
        _clearFieldsButton.Location = new System.Drawing.Point(310, 394);
        _clearFieldsButton.Name = "_clearFieldsButton";
        _clearFieldsButton.Size = new System.Drawing.Size(74, 30);
        _clearFieldsButton.TabIndex = 4;
        _clearFieldsButton.Text = "Clear";
        _clearFieldsButton.UseVisualStyleBackColor = true;
        _clearFieldsButton.Click += ClearFieldsButton_Click;

        // _fieldHintLabel
        _fieldHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _fieldHintLabel.Location = new System.Drawing.Point(18, 430);
        _fieldHintLabel.Name = "_fieldHintLabel";
        _fieldHintLabel.Size = new System.Drawing.Size(450, 44);
        _fieldHintLabel.TabIndex = 5;
        _fieldHintLabel.Text = "The order shown in each field list becomes the column order in that worksheet.";

        // _maintenanceTabPage
        _maintenanceTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceTabPage.Controls.Add(_maintenanceIntroLabel);
        _maintenanceTabPage.Controls.Add(_maintenanceSubTabControl);
        _maintenanceTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceTabPage.Name = "_maintenanceTabPage";
        _maintenanceTabPage.Padding = new Padding(16);
        _maintenanceTabPage.Size = new System.Drawing.Size(880, 572);
        _maintenanceTabPage.TabIndex = 3;
        _maintenanceTabPage.Text = "Maintenance";

        // _maintenanceIntroLabel
        _maintenanceIntroLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceIntroLabel.Location = new System.Drawing.Point(24, 18);
        _maintenanceIntroLabel.Name = "_maintenanceIntroLabel";
        _maintenanceIntroLabel.Size = new System.Drawing.Size(820, 24);
        _maintenanceIntroLabel.TabIndex = 0;
        _maintenanceIntroLabel.Text = "Maintenance tools are visible only when DeskPulse is started with -maintenance or -m.";

        // _maintenanceSubTabControl
        _maintenanceSubTabControl.Controls.Add(_maintenanceDatabaseTabPage);
        _maintenanceSubTabControl.Controls.Add(_maintenanceStatisticsTabPage);
        _maintenanceSubTabControl.Controls.Add(_maintenanceCleanupTabPage);
        _maintenanceSubTabControl.Controls.Add(_maintenanceLoggingRulesTabPage);
        _maintenanceSubTabControl.Controls.Add(_maintenanceDiagnosticsTabPage);
        _maintenanceSubTabControl.Location = new System.Drawing.Point(24, 48);
        _maintenanceSubTabControl.Name = "_maintenanceSubTabControl";
        _maintenanceSubTabControl.SelectedIndex = 0;
        _maintenanceSubTabControl.Size = new System.Drawing.Size(820, 506);
        _maintenanceSubTabControl.TabIndex = 1;

        // _maintenanceDatabaseTabPage
        _maintenanceDatabaseTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDatabaseTabPage.Controls.Add(_maintenanceDatabaseGroupBox);
        _maintenanceDatabaseTabPage.Controls.Add(_maintenanceDatabaseNotesGroupBox);
        _maintenanceDatabaseTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceDatabaseTabPage.Name = "_maintenanceDatabaseTabPage";
        _maintenanceDatabaseTabPage.Padding = new Padding(16);
        _maintenanceDatabaseTabPage.Size = new System.Drawing.Size(812, 478);
        _maintenanceDatabaseTabPage.TabIndex = 0;
        _maintenanceDatabaseTabPage.Text = "Database";

        // _maintenanceDatabaseGroupBox
        _maintenanceDatabaseGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDatabaseGroupBox.Controls.Add(_maintenanceDatabaseInfoTextBox);
        _maintenanceDatabaseGroupBox.Controls.Add(_maintenanceRefreshDatabaseButton);
        _maintenanceDatabaseGroupBox.Controls.Add(_maintenanceOpenDataFolderButton);
        _maintenanceDatabaseGroupBox.Controls.Add(_maintenanceOpenProgramFolderButton);
        _maintenanceDatabaseGroupBox.Location = new System.Drawing.Point(18, 20);
        _maintenanceDatabaseGroupBox.Name = "_maintenanceDatabaseGroupBox";
        _maintenanceDatabaseGroupBox.Padding = new Padding(12);
        _maintenanceDatabaseGroupBox.Size = new System.Drawing.Size(760, 258);
        _maintenanceDatabaseGroupBox.TabIndex = 0;
        _maintenanceDatabaseGroupBox.TabStop = false;
        _maintenanceDatabaseGroupBox.Text = "Database status";

        _maintenanceDatabaseInfoTextBox.Location = new System.Drawing.Point(18, 30);
        _maintenanceDatabaseInfoTextBox.Multiline = true;
        _maintenanceDatabaseInfoTextBox.Name = "_maintenanceDatabaseInfoTextBox";
        _maintenanceDatabaseInfoTextBox.ReadOnly = true;
        _maintenanceDatabaseInfoTextBox.ScrollBars = ScrollBars.Vertical;
        _maintenanceDatabaseInfoTextBox.Size = new System.Drawing.Size(722, 170);
        _maintenanceDatabaseInfoTextBox.TabIndex = 0;

        _maintenanceRefreshDatabaseButton.FlatStyle = FlatStyle.System;
        _maintenanceRefreshDatabaseButton.Location = new System.Drawing.Point(18, 214);
        _maintenanceRefreshDatabaseButton.Name = "_maintenanceRefreshDatabaseButton";
        _maintenanceRefreshDatabaseButton.Size = new System.Drawing.Size(100, 30);
        _maintenanceRefreshDatabaseButton.TabIndex = 1;
        _maintenanceRefreshDatabaseButton.Text = "Refresh";
        _maintenanceRefreshDatabaseButton.UseVisualStyleBackColor = true;
        _maintenanceRefreshDatabaseButton.Click += MaintenanceRefreshDatabaseButton_Click;

        _maintenanceOpenDataFolderButton.FlatStyle = FlatStyle.System;
        _maintenanceOpenDataFolderButton.Location = new System.Drawing.Point(132, 214);
        _maintenanceOpenDataFolderButton.Name = "_maintenanceOpenDataFolderButton";
        _maintenanceOpenDataFolderButton.Size = new System.Drawing.Size(160, 30);
        _maintenanceOpenDataFolderButton.TabIndex = 2;
        _maintenanceOpenDataFolderButton.Text = "Open Data Folder";
        _maintenanceOpenDataFolderButton.UseVisualStyleBackColor = true;
        _maintenanceOpenDataFolderButton.Click += MaintenanceOpenDataFolderButton_Click;

        _maintenanceOpenProgramFolderButton.FlatStyle = FlatStyle.System;
        _maintenanceOpenProgramFolderButton.Location = new System.Drawing.Point(306, 214);
        _maintenanceOpenProgramFolderButton.Name = "_maintenanceOpenProgramFolderButton";
        _maintenanceOpenProgramFolderButton.Size = new System.Drawing.Size(170, 30);
        _maintenanceOpenProgramFolderButton.TabIndex = 3;
        _maintenanceOpenProgramFolderButton.Text = "Open Program Folder";
        _maintenanceOpenProgramFolderButton.UseVisualStyleBackColor = true;
        _maintenanceOpenProgramFolderButton.Click += MaintenanceOpenProgramFolderButton_Click;

        // _maintenanceDatabaseNotesGroupBox
        _maintenanceDatabaseNotesGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDatabaseNotesGroupBox.Controls.Add(_maintenanceDatabaseNotesLabel);
        _maintenanceDatabaseNotesGroupBox.Location = new System.Drawing.Point(18, 298);
        _maintenanceDatabaseNotesGroupBox.Name = "_maintenanceDatabaseNotesGroupBox";
        _maintenanceDatabaseNotesGroupBox.Padding = new Padding(12);
        _maintenanceDatabaseNotesGroupBox.Size = new System.Drawing.Size(760, 130);
        _maintenanceDatabaseNotesGroupBox.TabIndex = 1;
        _maintenanceDatabaseNotesGroupBox.TabStop = false;
        _maintenanceDatabaseNotesGroupBox.Text = "Notes";

        _maintenanceDatabaseNotesLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceDatabaseNotesLabel.Location = new System.Drawing.Point(18, 30);
        _maintenanceDatabaseNotesLabel.Name = "_maintenanceDatabaseNotesLabel";
        _maintenanceDatabaseNotesLabel.Size = new System.Drawing.Size(710, 70);
        _maintenanceDatabaseNotesLabel.TabIndex = 0;
        _maintenanceDatabaseNotesLabel.Text = "SQLite stores TEXT fields using the actual stored text length. Empty/NULL fields are not fixed 100-byte fields. Database growth is mainly caused by repeated events, long paths, notes, window titles, and indexes.";

        // _maintenanceStatisticsTabPage
        _maintenanceStatisticsTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceStatisticsTabPage.Controls.Add(_maintenanceStatisticsTopLabel);
        _maintenanceStatisticsTabPage.Controls.Add(_statisticsViewComboBox);
        _maintenanceStatisticsTabPage.Controls.Add(_maintenanceStatisticsRefreshButton);
        _maintenanceStatisticsTabPage.Controls.Add(_maintenanceAddFileExclusionButton);
        _maintenanceStatisticsTabPage.Controls.Add(_maintenanceAddFolderExclusionButton);
        _maintenanceStatisticsTabPage.Controls.Add(_maintenanceAddProcessExclusionButton);
        _maintenanceStatisticsTabPage.Controls.Add(_statisticsGrid);
        _maintenanceStatisticsTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceStatisticsTabPage.Name = "_maintenanceStatisticsTabPage";
        _maintenanceStatisticsTabPage.Padding = new Padding(16);
        _maintenanceStatisticsTabPage.Size = new System.Drawing.Size(812, 478);
        _maintenanceStatisticsTabPage.TabIndex = 1;
        _maintenanceStatisticsTabPage.Text = "Statistics";

        _maintenanceStatisticsTopLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceStatisticsTopLabel.Location = new System.Drawing.Point(18, 18);
        _maintenanceStatisticsTopLabel.Name = "_maintenanceStatisticsTopLabel";
        _maintenanceStatisticsTopLabel.Size = new System.Drawing.Size(740, 24);
        _maintenanceStatisticsTopLabel.TabIndex = 0;
        _maintenanceStatisticsTopLabel.Text = "Use these Top 100 views to find noisy paths, folders, extensions, and processes that may need exclusions.";

        _statisticsViewComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statisticsViewComboBox.Items.AddRange(new object[] { "Top 100 full paths", "Top 100 folders", "Top 100 file processes", "Top 100 extensions", "Top 100 program events" });
        _statisticsViewComboBox.Location = new System.Drawing.Point(18, 50);
        _statisticsViewComboBox.Name = "_statisticsViewComboBox";
        _statisticsViewComboBox.Size = new System.Drawing.Size(260, 23);
        _statisticsViewComboBox.TabIndex = 1;
        _statisticsViewComboBox.SelectedIndexChanged += MaintenanceStatisticsViewComboBox_SelectedIndexChanged;

        _maintenanceStatisticsRefreshButton.FlatStyle = FlatStyle.System;
        _maintenanceStatisticsRefreshButton.Location = new System.Drawing.Point(292, 48);
        _maintenanceStatisticsRefreshButton.Name = "_maintenanceStatisticsRefreshButton";
        _maintenanceStatisticsRefreshButton.Size = new System.Drawing.Size(100, 30);
        _maintenanceStatisticsRefreshButton.TabIndex = 2;
        _maintenanceStatisticsRefreshButton.Text = "Refresh";
        _maintenanceStatisticsRefreshButton.UseVisualStyleBackColor = true;
        _maintenanceStatisticsRefreshButton.Click += MaintenanceStatisticsRefreshButton_Click;

        _maintenanceAddFileExclusionButton.FlatStyle = FlatStyle.System;
        _maintenanceAddFileExclusionButton.Location = new System.Drawing.Point(408, 48);
        _maintenanceAddFileExclusionButton.Name = "_maintenanceAddFileExclusionButton";
        _maintenanceAddFileExclusionButton.Size = new System.Drawing.Size(142, 30);
        _maintenanceAddFileExclusionButton.TabIndex = 3;
        _maintenanceAddFileExclusionButton.Text = "Add File Exclusion";
        _maintenanceAddFileExclusionButton.UseVisualStyleBackColor = true;
        _maintenanceAddFileExclusionButton.Click += MaintenanceAddFileExclusionButton_Click;

        _maintenanceAddFolderExclusionButton.FlatStyle = FlatStyle.System;
        _maintenanceAddFolderExclusionButton.Location = new System.Drawing.Point(558, 48);
        _maintenanceAddFolderExclusionButton.Name = "_maintenanceAddFolderExclusionButton";
        _maintenanceAddFolderExclusionButton.Size = new System.Drawing.Size(154, 30);
        _maintenanceAddFolderExclusionButton.TabIndex = 4;
        _maintenanceAddFolderExclusionButton.Text = "Add Folder Exclusion";
        _maintenanceAddFolderExclusionButton.UseVisualStyleBackColor = true;
        _maintenanceAddFolderExclusionButton.Click += MaintenanceAddFolderExclusionButton_Click;

        _maintenanceAddProcessExclusionButton.FlatStyle = FlatStyle.System;
        _maintenanceAddProcessExclusionButton.Location = new System.Drawing.Point(720, 48);
        _maintenanceAddProcessExclusionButton.Name = "_maintenanceAddProcessExclusionButton";
        _maintenanceAddProcessExclusionButton.Size = new System.Drawing.Size(100, 30);
        _maintenanceAddProcessExclusionButton.TabIndex = 5;
        _maintenanceAddProcessExclusionButton.Text = "Add Process";
        _maintenanceAddProcessExclusionButton.UseVisualStyleBackColor = true;
        _maintenanceAddProcessExclusionButton.Click += MaintenanceAddProcessExclusionButton_Click;

        _statisticsGrid.AllowUserToAddRows = false;
        _statisticsGrid.AllowUserToDeleteRows = false;
        _statisticsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _statisticsGrid.BackgroundColor = System.Drawing.SystemColors.Window;
        _statisticsGrid.BorderStyle = BorderStyle.FixedSingle;
        _statisticsGrid.Location = new System.Drawing.Point(18, 92);
        _statisticsGrid.MultiSelect = true;
        _statisticsGrid.Name = "_statisticsGrid";
        _statisticsGrid.ReadOnly = true;
        _statisticsGrid.RowHeadersVisible = false;
        _statisticsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _statisticsGrid.Size = new System.Drawing.Size(802, 336);
        _statisticsGrid.TabIndex = 6;

        // _maintenanceCleanupTabPage
        _maintenanceCleanupTabPage.AutoScroll = true;
        _maintenanceCleanupTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceCleanupTabPage.Controls.Add(_maintenanceGeneratedFilesGroupBox);
        _maintenanceCleanupTabPage.Controls.Add(_maintenanceUnwantedDataGroupBox);
        _maintenanceCleanupTabPage.Controls.Add(_maintenanceDatabaseCleanupGroupBox);
        _maintenanceCleanupTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceCleanupTabPage.Name = "_maintenanceCleanupTabPage";
        _maintenanceCleanupTabPage.Padding = new Padding(16);
        _maintenanceCleanupTabPage.Size = new System.Drawing.Size(812, 478);
        _maintenanceCleanupTabPage.TabIndex = 2;
        _maintenanceCleanupTabPage.Text = "Cleanup";

        _maintenanceGeneratedFilesGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceGeneratedFilesGroupBox.Controls.Add(_maintenanceDeleteExportButton);
        _maintenanceGeneratedFilesGroupBox.Controls.Add(_maintenanceDeleteDiagnosticsButton);
        _maintenanceGeneratedFilesGroupBox.Controls.Add(_maintenanceDeleteStartupLogButton);
        _maintenanceGeneratedFilesGroupBox.Controls.Add(_maintenanceGeneratedFilesHintLabel);
        _maintenanceGeneratedFilesGroupBox.Location = new System.Drawing.Point(18, 18);
        _maintenanceGeneratedFilesGroupBox.Name = "_maintenanceGeneratedFilesGroupBox";
        _maintenanceGeneratedFilesGroupBox.Size = new System.Drawing.Size(820, 146);
        _maintenanceGeneratedFilesGroupBox.TabIndex = 0;
        _maintenanceGeneratedFilesGroupBox.TabStop = false;
        _maintenanceGeneratedFilesGroupBox.Text = "Delete generated files only";

        _maintenanceDeleteExportButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteExportButton.Location = new System.Drawing.Point(18, 34);
        _maintenanceDeleteExportButton.Name = "_maintenanceDeleteExportButton";
        _maintenanceDeleteExportButton.Size = new System.Drawing.Size(150, 30);
        _maintenanceDeleteExportButton.TabIndex = 0;
        _maintenanceDeleteExportButton.Text = "Delete Excel Export";
        _maintenanceDeleteExportButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteExportButton.Click += MaintenanceDeleteExportButton_Click;

        _maintenanceDeleteDiagnosticsButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteDiagnosticsButton.Location = new System.Drawing.Point(184, 34);
        _maintenanceDeleteDiagnosticsButton.Name = "_maintenanceDeleteDiagnosticsButton";
        _maintenanceDeleteDiagnosticsButton.Size = new System.Drawing.Size(160, 30);
        _maintenanceDeleteDiagnosticsButton.TabIndex = 1;
        _maintenanceDeleteDiagnosticsButton.Text = "Delete Diagnostic Log";
        _maintenanceDeleteDiagnosticsButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteDiagnosticsButton.Click += MaintenanceDeleteDiagnosticsButton_Click;

        _maintenanceDeleteStartupLogButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteStartupLogButton.Location = new System.Drawing.Point(360, 34);
        _maintenanceDeleteStartupLogButton.Name = "_maintenanceDeleteStartupLogButton";
        _maintenanceDeleteStartupLogButton.Size = new System.Drawing.Size(150, 30);
        _maintenanceDeleteStartupLogButton.TabIndex = 2;
        _maintenanceDeleteStartupLogButton.Text = "Delete Startup Log";
        _maintenanceDeleteStartupLogButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteStartupLogButton.Click += MaintenanceDeleteStartupLogButton_Click;

        _maintenanceGeneratedFilesHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceGeneratedFilesHintLabel.Location = new System.Drawing.Point(18, 80);
        _maintenanceGeneratedFilesHintLabel.Name = "_maintenanceGeneratedFilesHintLabel";
        _maintenanceGeneratedFilesHintLabel.Size = new System.Drawing.Size(780, 36);
        _maintenanceGeneratedFilesHintLabel.TabIndex = 3;
        _maintenanceGeneratedFilesHintLabel.Text = "Generated-file cleanup is safe housekeeping for reports/log files only. It does not delete the database or activity records.";

        _maintenanceUnwantedDataGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceUnwantedDataGroupBox.Controls.Add(_maintenanceRemoveUnwantedDataButton);
        _maintenanceUnwantedDataGroupBox.Controls.Add(_maintenanceUnwantedDataHintLabel);
        _maintenanceUnwantedDataGroupBox.Location = new System.Drawing.Point(18, 180);
        _maintenanceUnwantedDataGroupBox.Name = "_maintenanceUnwantedDataGroupBox";
        _maintenanceUnwantedDataGroupBox.Size = new System.Drawing.Size(820, 126);
        _maintenanceUnwantedDataGroupBox.TabIndex = 1;
        _maintenanceUnwantedDataGroupBox.TabStop = false;
        _maintenanceUnwantedDataGroupBox.Text = "Remove unwanted data using Logging Rules";

        _maintenanceRemoveUnwantedDataButton.FlatStyle = FlatStyle.System;
        _maintenanceRemoveUnwantedDataButton.Location = new System.Drawing.Point(18, 34);
        _maintenanceRemoveUnwantedDataButton.Name = "_maintenanceRemoveUnwantedDataButton";
        _maintenanceRemoveUnwantedDataButton.Size = new System.Drawing.Size(190, 30);
        _maintenanceRemoveUnwantedDataButton.TabIndex = 0;
        _maintenanceRemoveUnwantedDataButton.Text = "Remove Unwanted Data...";
        _maintenanceRemoveUnwantedDataButton.UseVisualStyleBackColor = true;
        _maintenanceRemoveUnwantedDataButton.Click += MaintenanceRemoveUnwantedDataButton_Click;

        _maintenanceUnwantedDataHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceUnwantedDataHintLabel.Location = new System.Drawing.Point(224, 36);
        _maintenanceUnwantedDataHintLabel.Name = "_maintenanceUnwantedDataHintLabel";
        _maintenanceUnwantedDataHintLabel.Size = new System.Drawing.Size(570, 56);
        _maintenanceUnwantedDataHintLabel.TabIndex = 1;
        _maintenanceUnwantedDataHintLabel.Text = "Use this after creating Exclude rules for noisy files, folders, or processes. It removes only matching past file/program records and keeps the rules themselves.";

        _maintenanceDatabaseCleanupGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDatabaseCleanupGroupBox.Controls.Add(_maintenanceDeleteFileActivityButton);
        _maintenanceDatabaseCleanupGroupBox.Controls.Add(_maintenanceDeleteUserActivityButton);
        _maintenanceDatabaseCleanupGroupBox.Controls.Add(_maintenanceDeleteProgramActivityButton);
        _maintenanceDatabaseCleanupGroupBox.Controls.Add(_maintenanceDeleteAllActivityButton);
        _maintenanceDatabaseCleanupGroupBox.Controls.Add(_maintenanceDatabaseCleanupHintLabel);
        _maintenanceDatabaseCleanupGroupBox.Location = new System.Drawing.Point(18, 322);
        _maintenanceDatabaseCleanupGroupBox.Name = "_maintenanceDatabaseCleanupGroupBox";
        _maintenanceDatabaseCleanupGroupBox.Size = new System.Drawing.Size(820, 190);
        _maintenanceDatabaseCleanupGroupBox.TabIndex = 2;
        _maintenanceDatabaseCleanupGroupBox.TabStop = false;
        _maintenanceDatabaseCleanupGroupBox.Text = "Delete all records by activity type";

        _maintenanceDeleteFileActivityButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteFileActivityButton.Location = new System.Drawing.Point(18, 34);
        _maintenanceDeleteFileActivityButton.Name = "_maintenanceDeleteFileActivityButton";
        _maintenanceDeleteFileActivityButton.Size = new System.Drawing.Size(170, 30);
        _maintenanceDeleteFileActivityButton.TabIndex = 0;
        _maintenanceDeleteFileActivityButton.Text = "Delete All File Activity";
        _maintenanceDeleteFileActivityButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteFileActivityButton.Click += MaintenanceDeleteFileActivityButton_Click;

        _maintenanceDeleteUserActivityButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteUserActivityButton.Location = new System.Drawing.Point(204, 34);
        _maintenanceDeleteUserActivityButton.Name = "_maintenanceDeleteUserActivityButton";
        _maintenanceDeleteUserActivityButton.Size = new System.Drawing.Size(210, 30);
        _maintenanceDeleteUserActivityButton.TabIndex = 1;
        _maintenanceDeleteUserActivityButton.Text = "Delete All User/Session Activity";
        _maintenanceDeleteUserActivityButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteUserActivityButton.Click += MaintenanceDeleteUserActivityButton_Click;

        _maintenanceDeleteProgramActivityButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteProgramActivityButton.Location = new System.Drawing.Point(430, 34);
        _maintenanceDeleteProgramActivityButton.Name = "_maintenanceDeleteProgramActivityButton";
        _maintenanceDeleteProgramActivityButton.Size = new System.Drawing.Size(190, 30);
        _maintenanceDeleteProgramActivityButton.TabIndex = 2;
        _maintenanceDeleteProgramActivityButton.Text = "Delete All Program Activity";
        _maintenanceDeleteProgramActivityButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteProgramActivityButton.Click += MaintenanceDeleteProgramActivityButton_Click;

        _maintenanceDeleteAllActivityButton.FlatStyle = FlatStyle.System;
        _maintenanceDeleteAllActivityButton.Location = new System.Drawing.Point(636, 34);
        _maintenanceDeleteAllActivityButton.Name = "_maintenanceDeleteAllActivityButton";
        _maintenanceDeleteAllActivityButton.Size = new System.Drawing.Size(170, 30);
        _maintenanceDeleteAllActivityButton.TabIndex = 3;
        _maintenanceDeleteAllActivityButton.Text = "Delete ALL Activity Records";
        _maintenanceDeleteAllActivityButton.UseVisualStyleBackColor = true;
        _maintenanceDeleteAllActivityButton.Click += MaintenanceDeleteAllActivityButton_Click;

        _maintenanceDatabaseCleanupHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceDatabaseCleanupHintLabel.Location = new System.Drawing.Point(18, 78);
        _maintenanceDatabaseCleanupHintLabel.Name = "_maintenanceDatabaseCleanupHintLabel";
        _maintenanceDatabaseCleanupHintLabel.Size = new System.Drawing.Size(780, 84);
        _maintenanceDatabaseCleanupHintLabel.TabIndex = 4;
        _maintenanceDatabaseCleanupHintLabel.Text = "These buttons permanently delete complete categories of stored activity records. They do not delete the SQLite database file, table structure, indexes, settings, logging rules, monitored extensions, or export options.";

        // _maintenanceLoggingRulesTabPage
        _maintenanceLoggingRulesTabPage.AutoScroll = true;
        _maintenanceLoggingRulesTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceLoggingRulesTabPage.Controls.Add(_maintenanceAddRuleGroupBox);
        _maintenanceLoggingRulesTabPage.Controls.Add(_maintenanceRulesGroupBox);
        _maintenanceLoggingRulesTabPage.Controls.Add(_maintenancePastRecordsGroupBox);
        _maintenanceLoggingRulesTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceLoggingRulesTabPage.Name = "_maintenanceLoggingRulesTabPage";
        _maintenanceLoggingRulesTabPage.Padding = new Padding(16);
        _maintenanceLoggingRulesTabPage.Size = new System.Drawing.Size(812, 478);
        _maintenanceLoggingRulesTabPage.TabIndex = 3;
        _maintenanceLoggingRulesTabPage.Text = "Logging Rules";

        _maintenanceAddRuleGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleFolderRadioButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleFileRadioButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleProcessRadioButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleExcludeRadioButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleIncludeRadioButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualRuleSubfoldersCheckBox);
        _maintenanceAddRuleGroupBox.Controls.Add(_manualLoggingRuleTextBox);
        _maintenanceAddRuleGroupBox.Controls.Add(_maintenanceBrowseRuleButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_maintenanceAddRuleButton);
        _maintenanceAddRuleGroupBox.Controls.Add(_maintenanceAddRuleHintLabel);
        _maintenanceAddRuleGroupBox.Location = new System.Drawing.Point(14, 14);
        _maintenanceAddRuleGroupBox.Name = "_maintenanceAddRuleGroupBox";
        _maintenanceAddRuleGroupBox.Size = new System.Drawing.Size(828, 132);
        _maintenanceAddRuleGroupBox.TabIndex = 0;
        _maintenanceAddRuleGroupBox.TabStop = false;
        _maintenanceAddRuleGroupBox.Text = "Add logging rule";

        _manualRuleFolderRadioButton.Checked = true;
        _manualRuleFolderRadioButton.Location = new System.Drawing.Point(18, 28);
        _manualRuleFolderRadioButton.Name = "_manualRuleFolderRadioButton";
        _manualRuleFolderRadioButton.Size = new System.Drawing.Size(80, 22);
        _manualRuleFolderRadioButton.Text = "Folder";
        _manualRuleFolderRadioButton.UseVisualStyleBackColor = true;
        _manualRuleFolderRadioButton.CheckedChanged += ManualRuleTypeRadioButton_CheckedChanged;

        _manualRuleFileRadioButton.Location = new System.Drawing.Point(98, 28);
        _manualRuleFileRadioButton.Name = "_manualRuleFileRadioButton";
        _manualRuleFileRadioButton.Size = new System.Drawing.Size(58, 22);
        _manualRuleFileRadioButton.Text = "File";
        _manualRuleFileRadioButton.UseVisualStyleBackColor = true;
        _manualRuleFileRadioButton.CheckedChanged += ManualRuleTypeRadioButton_CheckedChanged;

        _manualRuleProcessRadioButton.Location = new System.Drawing.Point(162, 28);
        _manualRuleProcessRadioButton.Name = "_manualRuleProcessRadioButton";
        _manualRuleProcessRadioButton.Size = new System.Drawing.Size(130, 22);
        _manualRuleProcessRadioButton.Text = "Process / program";
        _manualRuleProcessRadioButton.UseVisualStyleBackColor = true;
        _manualRuleProcessRadioButton.CheckedChanged += ManualRuleTypeRadioButton_CheckedChanged;

        _manualRuleExcludeRadioButton.Checked = true;
        _manualRuleExcludeRadioButton.Location = new System.Drawing.Point(310, 28);
        _manualRuleExcludeRadioButton.Name = "_manualRuleExcludeRadioButton";
        _manualRuleExcludeRadioButton.Size = new System.Drawing.Size(80, 22);
        _manualRuleExcludeRadioButton.Text = "Exclude";
        _manualRuleExcludeRadioButton.UseVisualStyleBackColor = true;

        _manualRuleIncludeRadioButton.Location = new System.Drawing.Point(400, 28);
        _manualRuleIncludeRadioButton.Name = "_manualRuleIncludeRadioButton";
        _manualRuleIncludeRadioButton.Size = new System.Drawing.Size(120, 22);
        _manualRuleIncludeRadioButton.Text = "Include / allow";
        _manualRuleIncludeRadioButton.UseVisualStyleBackColor = true;

        _manualRuleSubfoldersCheckBox.Checked = true;
        _manualRuleSubfoldersCheckBox.Location = new System.Drawing.Point(550, 28);
        _manualRuleSubfoldersCheckBox.Name = "_manualRuleSubfoldersCheckBox";
        _manualRuleSubfoldersCheckBox.Size = new System.Drawing.Size(150, 22);
        _manualRuleSubfoldersCheckBox.Text = "Include subfolders";
        _manualRuleSubfoldersCheckBox.UseVisualStyleBackColor = true;

        _manualLoggingRuleTextBox.Location = new System.Drawing.Point(18, 66);
        _manualLoggingRuleTextBox.Name = "_manualLoggingRuleTextBox";
        _manualLoggingRuleTextBox.PlaceholderText = "Folder path, exact file path, or process name / .exe path";
        _manualLoggingRuleTextBox.Size = new System.Drawing.Size(598, 23);

        _maintenanceBrowseRuleButton.FlatStyle = FlatStyle.System;
        _maintenanceBrowseRuleButton.Location = new System.Drawing.Point(628, 63);
        _maintenanceBrowseRuleButton.Name = "_maintenanceBrowseRuleButton";
        _maintenanceBrowseRuleButton.Size = new System.Drawing.Size(90, 30);
        _maintenanceBrowseRuleButton.TabIndex = 7;
        _maintenanceBrowseRuleButton.Text = "Browse...";
        _maintenanceBrowseRuleButton.UseVisualStyleBackColor = true;
        _maintenanceBrowseRuleButton.Click += MaintenanceBrowseRuleButton_Click;

        _maintenanceAddRuleButton.FlatStyle = FlatStyle.System;
        _maintenanceAddRuleButton.Location = new System.Drawing.Point(728, 63);
        _maintenanceAddRuleButton.Name = "_maintenanceAddRuleButton";
        _maintenanceAddRuleButton.Size = new System.Drawing.Size(82, 30);
        _maintenanceAddRuleButton.TabIndex = 8;
        _maintenanceAddRuleButton.Text = "Add Rule";
        _maintenanceAddRuleButton.UseVisualStyleBackColor = true;
        _maintenanceAddRuleButton.Click += MaintenanceAddRuleButton_Click;

        _maintenanceAddRuleHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceAddRuleHintLabel.Location = new System.Drawing.Point(18, 100);
        _maintenanceAddRuleHintLabel.Name = "_maintenanceAddRuleHintLabel";
        _maintenanceAddRuleHintLabel.Size = new System.Drawing.Size(780, 22);
        _maintenanceAddRuleHintLabel.Text = "Folder rules use Browse for a folder. File rules use Browse for an exact file. Process rules use Browse for an .exe or a process name such as acad.exe.";

        _maintenanceRulesGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceRulesGroupBox.Controls.Add(_loggingRulesGridView);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceMoveRuleUpButton);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceMoveRuleDownButton);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceRemoveRuleButton);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceDuplicateRuleButton);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceResetRulesButton);
        _maintenanceRulesGroupBox.Controls.Add(_maintenanceRulesHintLabel);
        _maintenanceRulesGroupBox.Location = new System.Drawing.Point(14, 158);
        _maintenanceRulesGroupBox.Name = "_maintenanceRulesGroupBox";
        _maintenanceRulesGroupBox.Size = new System.Drawing.Size(828, 304);
        _maintenanceRulesGroupBox.TabIndex = 1;
        _maintenanceRulesGroupBox.TabStop = false;
        _maintenanceRulesGroupBox.Text = "Logging rules";

        _loggingRulesGridView.AllowUserToAddRows = false;
        _loggingRulesGridView.AllowUserToDeleteRows = true;
        _loggingRulesGridView.AllowUserToResizeRows = false;
        _loggingRulesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _loggingRulesGridView.BackgroundColor = System.Drawing.SystemColors.Window;
        _loggingRulesGridView.EditMode = DataGridViewEditMode.EditOnEnter;
        _loggingRulesGridView.Location = new System.Drawing.Point(18, 30);
        _loggingRulesGridView.MultiSelect = false;
        _loggingRulesGridView.Name = "_loggingRulesGridView";
        _loggingRulesGridView.RowHeadersVisible = false;
        _loggingRulesGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _loggingRulesGridView.Size = new System.Drawing.Size(790, 190);
        _loggingRulesGridView.TabIndex = 0;

        _maintenanceMoveRuleUpButton.FlatStyle = FlatStyle.System;
        _maintenanceMoveRuleUpButton.Location = new System.Drawing.Point(18, 230);
        _maintenanceMoveRuleUpButton.Name = "_maintenanceMoveRuleUpButton";
        _maintenanceMoveRuleUpButton.Size = new System.Drawing.Size(82, 30);
        _maintenanceMoveRuleUpButton.Text = "Move Up";
        _maintenanceMoveRuleUpButton.UseVisualStyleBackColor = true;
        _maintenanceMoveRuleUpButton.Click += MaintenanceMoveRuleUpButton_Click;

        _maintenanceMoveRuleDownButton.FlatStyle = FlatStyle.System;
        _maintenanceMoveRuleDownButton.Location = new System.Drawing.Point(108, 230);
        _maintenanceMoveRuleDownButton.Name = "_maintenanceMoveRuleDownButton";
        _maintenanceMoveRuleDownButton.Size = new System.Drawing.Size(92, 30);
        _maintenanceMoveRuleDownButton.Text = "Move Down";
        _maintenanceMoveRuleDownButton.UseVisualStyleBackColor = true;
        _maintenanceMoveRuleDownButton.Click += MaintenanceMoveRuleDownButton_Click;

        _maintenanceRemoveRuleButton.FlatStyle = FlatStyle.System;
        _maintenanceRemoveRuleButton.Location = new System.Drawing.Point(208, 230);
        _maintenanceRemoveRuleButton.Name = "_maintenanceRemoveRuleButton";
        _maintenanceRemoveRuleButton.Size = new System.Drawing.Size(82, 30);
        _maintenanceRemoveRuleButton.Text = "Remove";
        _maintenanceRemoveRuleButton.UseVisualStyleBackColor = true;
        _maintenanceRemoveRuleButton.Click += MaintenanceRemoveRuleButton_Click;

        _maintenanceDuplicateRuleButton.FlatStyle = FlatStyle.System;
        _maintenanceDuplicateRuleButton.Location = new System.Drawing.Point(298, 230);
        _maintenanceDuplicateRuleButton.Name = "_maintenanceDuplicateRuleButton";
        _maintenanceDuplicateRuleButton.Size = new System.Drawing.Size(88, 30);
        _maintenanceDuplicateRuleButton.Text = "Duplicate";
        _maintenanceDuplicateRuleButton.UseVisualStyleBackColor = true;
        _maintenanceDuplicateRuleButton.Click += MaintenanceDuplicateRuleButton_Click;

        _maintenanceResetRulesButton.FlatStyle = FlatStyle.System;
        _maintenanceResetRulesButton.Location = new System.Drawing.Point(398, 230);
        _maintenanceResetRulesButton.Name = "_maintenanceResetRulesButton";
        _maintenanceResetRulesButton.Size = new System.Drawing.Size(112, 30);
        _maintenanceResetRulesButton.Text = "Reset Defaults";
        _maintenanceResetRulesButton.UseVisualStyleBackColor = true;
        _maintenanceResetRulesButton.Click += MaintenanceResetRulesButton_Click;

        _maintenanceRulesHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceRulesHintLabel.Location = new System.Drawing.Point(18, 268);
        _maintenanceRulesHintLabel.Name = "_maintenanceRulesHintLabel";
        _maintenanceRulesHintLabel.Size = new System.Drawing.Size(760, 22);
        _maintenanceRulesHintLabel.Text = "Rules are checked from top to bottom. The first matching rule wins. Put Include exceptions above broad Exclude rules.";

        _maintenancePastRecordsGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenancePastRecordsGroupBox.Controls.Add(_maintenanceRemovePastRecordsButton);
        _maintenancePastRecordsGroupBox.Controls.Add(_maintenancePastRecordsHintLabel);
        _maintenancePastRecordsGroupBox.Location = new System.Drawing.Point(14, 474);
        _maintenancePastRecordsGroupBox.Name = "_maintenancePastRecordsGroupBox";
        _maintenancePastRecordsGroupBox.Size = new System.Drawing.Size(828, 86);
        _maintenancePastRecordsGroupBox.TabIndex = 2;
        _maintenancePastRecordsGroupBox.TabStop = false;
        _maintenancePastRecordsGroupBox.Text = "Past records";

        _maintenanceRemovePastRecordsButton.FlatStyle = FlatStyle.System;
        _maintenanceRemovePastRecordsButton.Location = new System.Drawing.Point(18, 32);
        _maintenanceRemovePastRecordsButton.Name = "_maintenanceRemovePastRecordsButton";
        _maintenanceRemovePastRecordsButton.Size = new System.Drawing.Size(230, 30);
        _maintenanceRemovePastRecordsButton.Text = "Remove Matching Past Records...";
        _maintenanceRemovePastRecordsButton.UseVisualStyleBackColor = true;
        _maintenanceRemovePastRecordsButton.Click += MaintenanceRemovePastRecordsButton_Click;

        _maintenancePastRecordsHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenancePastRecordsHintLabel.Location = new System.Drawing.Point(264, 36);
        _maintenancePastRecordsHintLabel.Name = "_maintenancePastRecordsHintLabel";
        _maintenancePastRecordsHintLabel.Size = new System.Drawing.Size(520, 24);
        _maintenancePastRecordsHintLabel.Text = "Deletes existing file/program records that match current Exclude rules. It does not delete the rules themselves.";

        // _maintenanceDiagnosticsTabPage
        _maintenanceDiagnosticsTabPage.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDiagnosticsTabPage.Controls.Add(_maintenanceDiagnosticsGroupBox);
        _maintenanceDiagnosticsTabPage.Controls.Add(_maintenanceRegistryGroupBox);
        _maintenanceDiagnosticsTabPage.Location = new System.Drawing.Point(4, 24);
        _maintenanceDiagnosticsTabPage.Name = "_maintenanceDiagnosticsTabPage";
        _maintenanceDiagnosticsTabPage.Padding = new Padding(16);
        _maintenanceDiagnosticsTabPage.Size = new System.Drawing.Size(812, 478);
        _maintenanceDiagnosticsTabPage.TabIndex = 4;
        _maintenanceDiagnosticsTabPage.Text = "Diagnostics";

        _maintenanceDiagnosticsGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceDiagnosticsGroupBox.Controls.Add(_maintenanceDiagnosticsStatusLabel);
        _maintenanceDiagnosticsGroupBox.Controls.Add(_maintenanceOpenDiagnosticsLogButton);
        _maintenanceDiagnosticsGroupBox.Controls.Add(_maintenanceShowActiveExtensionsButton);
        _maintenanceDiagnosticsGroupBox.Controls.Add(_maintenanceShowStartupStatusButton);
        _maintenanceDiagnosticsGroupBox.Location = new System.Drawing.Point(18, 20);
        _maintenanceDiagnosticsGroupBox.Name = "_maintenanceDiagnosticsGroupBox";
        _maintenanceDiagnosticsGroupBox.Size = new System.Drawing.Size(760, 186);
        _maintenanceDiagnosticsGroupBox.TabIndex = 0;
        _maintenanceDiagnosticsGroupBox.TabStop = false;
        _maintenanceDiagnosticsGroupBox.Text = "Diagnostics";

        _maintenanceDiagnosticsStatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceDiagnosticsStatusLabel.Location = new System.Drawing.Point(18, 30);
        _maintenanceDiagnosticsStatusLabel.Name = "_maintenanceDiagnosticsStatusLabel";
        _maintenanceDiagnosticsStatusLabel.Size = new System.Drawing.Size(700, 40);
        _maintenanceDiagnosticsStatusLabel.Text = "Diagnostic logging status is loaded when the form opens.";

        _maintenanceOpenDiagnosticsLogButton.FlatStyle = FlatStyle.System;
        _maintenanceOpenDiagnosticsLogButton.Location = new System.Drawing.Point(18, 82);
        _maintenanceOpenDiagnosticsLogButton.Name = "_maintenanceOpenDiagnosticsLogButton";
        _maintenanceOpenDiagnosticsLogButton.Size = new System.Drawing.Size(170, 30);
        _maintenanceOpenDiagnosticsLogButton.Text = "Open Diagnostic Log";
        _maintenanceOpenDiagnosticsLogButton.UseVisualStyleBackColor = true;
        _maintenanceOpenDiagnosticsLogButton.Click += MaintenanceOpenDiagnosticsLogButton_Click;

        _maintenanceShowActiveExtensionsButton.FlatStyle = FlatStyle.System;
        _maintenanceShowActiveExtensionsButton.Location = new System.Drawing.Point(202, 82);
        _maintenanceShowActiveExtensionsButton.Name = "_maintenanceShowActiveExtensionsButton";
        _maintenanceShowActiveExtensionsButton.Size = new System.Drawing.Size(180, 30);
        _maintenanceShowActiveExtensionsButton.Text = "Show Active Extensions";
        _maintenanceShowActiveExtensionsButton.UseVisualStyleBackColor = true;
        _maintenanceShowActiveExtensionsButton.Click += MaintenanceShowActiveExtensionsButton_Click;

        _maintenanceShowStartupStatusButton.FlatStyle = FlatStyle.System;
        _maintenanceShowStartupStatusButton.Location = new System.Drawing.Point(398, 82);
        _maintenanceShowStartupStatusButton.Name = "_maintenanceShowStartupStatusButton";
        _maintenanceShowStartupStatusButton.Size = new System.Drawing.Size(170, 30);
        _maintenanceShowStartupStatusButton.Text = "Show Startup Status";
        _maintenanceShowStartupStatusButton.UseVisualStyleBackColor = true;
        _maintenanceShowStartupStatusButton.Click += MaintenanceShowStartupStatusButton_Click;

        _maintenanceRegistryGroupBox.BackColor = System.Drawing.SystemColors.Window;
        _maintenanceRegistryGroupBox.Controls.Add(_maintenanceRegistryPathLabel);
        _maintenanceRegistryGroupBox.Controls.Add(_maintenanceRemoveRegistrySettingsButton);
        _maintenanceRegistryGroupBox.Controls.Add(_maintenanceRegistryHintLabel);
        _maintenanceRegistryGroupBox.Location = new System.Drawing.Point(18, 226);
        _maintenanceRegistryGroupBox.Name = "_maintenanceRegistryGroupBox";
        _maintenanceRegistryGroupBox.Size = new System.Drawing.Size(760, 156);
        _maintenanceRegistryGroupBox.TabIndex = 1;
        _maintenanceRegistryGroupBox.TabStop = false;
        _maintenanceRegistryGroupBox.Text = "Registry";

        _maintenanceRegistryPathLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceRegistryPathLabel.Location = new System.Drawing.Point(18, 30);
        _maintenanceRegistryPathLabel.Name = "_maintenanceRegistryPathLabel";
        _maintenanceRegistryPathLabel.Size = new System.Drawing.Size(700, 22);
        _maintenanceRegistryPathLabel.Text = "Registry settings location: HKCU\\Software\\DeskPulse";

        _maintenanceRemoveRegistrySettingsButton.FlatStyle = FlatStyle.System;
        _maintenanceRemoveRegistrySettingsButton.Location = new System.Drawing.Point(18, 68);
        _maintenanceRemoveRegistrySettingsButton.Name = "_maintenanceRemoveRegistrySettingsButton";
        _maintenanceRemoveRegistrySettingsButton.Size = new System.Drawing.Size(190, 30);
        _maintenanceRemoveRegistrySettingsButton.Text = "Remove Registry Settings";
        _maintenanceRemoveRegistrySettingsButton.UseVisualStyleBackColor = true;
        _maintenanceRemoveRegistrySettingsButton.Click += MaintenanceRemoveRegistrySettingsButton_Click;

        _maintenanceRegistryHintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _maintenanceRegistryHintLabel.Location = new System.Drawing.Point(226, 70);
        _maintenanceRegistryHintLabel.Name = "_maintenanceRegistryHintLabel";
        _maintenanceRegistryHintLabel.Size = new System.Drawing.Size(500, 44);
        _maintenanceRegistryHintLabel.Text = "Removes current-user settings only. It does not delete the app, database, Excel export, or other files.";

        // _footerLine
        _footerLine.BorderStyle = BorderStyle.Fixed3D;
        _footerLine.Location = new System.Drawing.Point(16, 626);
        _footerLine.Name = "_footerLine";
        _footerLine.Size = new System.Drawing.Size(888, 1);
        _footerLine.TabIndex = 1;

        // _saveButton
        _saveButton.DialogResult = DialogResult.OK;
        _saveButton.FlatStyle = FlatStyle.System;
        _saveButton.Location = new System.Drawing.Point(724, 644);
        _saveButton.Name = "_saveButton";
        _saveButton.Size = new System.Drawing.Size(84, 30);
        _saveButton.TabIndex = 2;
        _saveButton.Text = "Save";
        _saveButton.UseVisualStyleBackColor = true;
        _saveButton.Click += SaveButton_Click;

        // _cancelButton
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.FlatStyle = FlatStyle.System;
        _cancelButton.Location = new System.Drawing.Point(820, 644);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Size = new System.Drawing.Size(84, 30);
        _cancelButton.TabIndex = 3;
        _cancelButton.Text = "Cancel";
        _cancelButton.UseVisualStyleBackColor = true;

        // SettingsForm
        AcceptButton = _saveButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Control;
        CancelButton = _cancelButton;
        ClientSize = new System.Drawing.Size(920, 690);
        Controls.Add(_settingsTabControl);
        Controls.Add(_footerLine);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
        Font = System.Drawing.SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "DeskPulse Settings";

        _maintenanceRegistryGroupBox.ResumeLayout(false);
        _maintenanceDiagnosticsGroupBox.ResumeLayout(false);
        _maintenancePastRecordsGroupBox.ResumeLayout(false);
        ((ISupportInitialize)_loggingRulesGridView).EndInit();
        _maintenanceRulesGroupBox.ResumeLayout(false);
        _maintenanceAddRuleGroupBox.ResumeLayout(false);
        _maintenanceAddRuleGroupBox.PerformLayout();
        _maintenanceDatabaseCleanupGroupBox.ResumeLayout(false);
        _maintenanceUnwantedDataGroupBox.ResumeLayout(false);
        _maintenanceGeneratedFilesGroupBox.ResumeLayout(false);
        ((ISupportInitialize)_statisticsGrid).EndInit();
        _maintenanceDatabaseNotesGroupBox.ResumeLayout(false);
        _maintenanceDatabaseGroupBox.ResumeLayout(false);
        _maintenanceDatabaseGroupBox.PerformLayout();
        _maintenanceDiagnosticsTabPage.ResumeLayout(false);
        _maintenanceLoggingRulesTabPage.ResumeLayout(false);
        _maintenanceCleanupTabPage.ResumeLayout(false);
        _maintenanceStatisticsTabPage.ResumeLayout(false);
        _maintenanceDatabaseTabPage.ResumeLayout(false);
        _maintenanceSubTabControl.ResumeLayout(false);
        _maintenanceTabPage.ResumeLayout(false);
        _worksheetColumnsGroupBox.ResumeLayout(false);
        _worksheetsGroupBox.ResumeLayout(false);
        _fileTypesGroupBox.ResumeLayout(false);
        _fileFilterGroupBox.ResumeLayout(false);
        _storageGroupBox.ResumeLayout(false);
        _storageGroupBox.PerformLayout();
        _behaviourGroupBox.ResumeLayout(false);
        _startupGroupBox.ResumeLayout(false);
        _exportOptionsTabPage.ResumeLayout(false);
        _filesTabPage.ResumeLayout(false);
        _generalTabPage.ResumeLayout(false);
        _settingsTabControl.ResumeLayout(false);
        ResumeLayout(false);
    }
}
