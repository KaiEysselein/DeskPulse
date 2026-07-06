# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It quietly tracks selected activity on a Windows PC and stores the records in a local SQLite database. Excel is used only for exports and reports; it is not the live log file.

## Version

Current version: `0.1.1`


## Release Status

Version `0.1.1` is the locked maintenance/data-management release baseline. Future feature changes and UI improvements should be tracked as `0.1.2` work unless a critical `0.1.1` hotfix is required.

## What DeskPulse Logs

DeskPulse can log:

- selected file open/write/close activity
- DeskPulse start/stop activity
- PC lock/unlock and Windows session activity
- program start/close activity in the current interactive Windows session

Email read/sent logging is not included in version `0.1.1`.

## Main Features

- Windows tray application
- quiet normal startup with no success popups
- left-click tray menu for daily actions:
  - `Export Activity Log`
  - `Settings...`
- right-click tray menu for secondary actions:
  - `About`
  - `Maintenance...` when started in maintenance mode
  - `Exit`
- ETW-based file activity monitoring for selected file extensions
- SQLite live storage using `DeskPulse.db`
- XLSX export/reporting using `DeskPulse-export.xlsx`
- calendar-only date-range export form
- export defaults to the current day
- real percentage export progress based on rows written
- configurable Excel worksheets and fields
- program start/close activity logging option
- Windows logon startup option using Task Scheduler
- dedicated Maintenance form available from the right-click menu only when started with `-maintenance` or `-m`
- right-click `Maintenance...` shortcut when started in maintenance mode
- portable cleanup/uninstall mode using `-uninstall`

## Data Location

Default data folder:

```text
%USERPROFILE%\Documents\DeskPulse\
```

Default SQLite database:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Default Excel export:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx
```

Diagnostic log when `-debug` is used:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-diagnostics.log
```

Startup fallback error log:

```text
%TEMP%\DeskPulse-startup.log
```

## Registry Settings

DeskPulse stores current-user settings here:

```text
HKCU\Software\DeskPulse
```

Important values include:

```text
AppVersion
DataFolderPath
DatabaseFilePath
ExcelExportFilePath
ExtensionsToMonitor
ExportSheets
IgnoreTempFolders
StartWithWindows
LogProgramActivity
```

## Settings Tabs

### General

The `General` tab contains general application options, including:

- start DeskPulse when the user logs in to Windows
- log program start and close activity

The Windows startup option creates or removes a Task Scheduler task named `DeskPulse`. The task uses an `ONLOGON` trigger and is configured to run with highest privileges.

### Files

The `Files` tab controls:

- DeskPulse data folder
- temporary-folder exclusion
- monitored file extensions

The monitored file-type list is the source of truth for file monitoring.

### Export Options

The `Export Options` tab controls which Excel worksheets are created, their order, and which fields/columns each worksheet contains.

Available worksheet options:

- `File Activity`
- `Daily Summary`
- `Summary by Extension`
- `Summary by Process`
- `Errors`
- `User`
- `Programs`

The default export remains `File Activity` only unless the user changes the export options.

### Maintenance

The `Maintenance` tab is hidden during normal use. To show it, start DeskPulse with:

```powershell
DeskPulse.exe -maintenance
```

Short form:

```powershell
DeskPulse.exe -m
```

Also accepted:

```text
--maintenance
/maintenance
--m
/m
```


When DeskPulse is started in maintenance mode, the right-click tray menu also shows:

```text
Maintenance...
```

This opens Settings directly on the Maintenance tab.

The Maintenance tab is for portable-app administration and cleanup. It includes tools such as:

- open the DeskPulse data folder
- open the DeskPulse program folder
- open the diagnostic log
- show active monitored extensions
- remove current-user registry settings


## Version 0.1.1 Maintenance Additions

Version `0.1.1` adds a larger hidden Maintenance area with internal sub-tabs:

- `Database` — database path, database-related file size, and record counts
- `Statistics` — Top 100 views for full paths, folders, file processes, extensions, and program events
- `Cleanup` — clear selected database record groups while preserving the database structure, and delete generated export/log files only
- `Logging Rules` — ordered include/exclude rules for folders, exact files, and processes/programs
- `Diagnostics` — diagnostic log, active extensions, startup task status, and registry cleanup

The Top 100 statistics are intended to identify noisy records that can be excluded safely in future use.

The `Logging Rules` maintenance sub-tab includes a `Remove Matching Past Records` action. This permanently deletes existing file/program database records that match the current exclude rules, including exact file exclusions. It shows a warning first, does not delete the rules themselves, and does not delete user/session records. The action uses a determinate progress bar: 1% while matching records are determined, then the remaining 99% based on actual deleted records.

## Export Activity Log

Use the left-click tray menu item:

```text
Export Activity Log
```

The export dialog shows two calendars only:

- start day
- end day

Both default to today. The selected date range is inclusive.

During export, DeskPulse shows a real percentage progress bar. The status text uses these stages:

```text
1%   Reading activity records
3%   Counting rows to export
5-93% Writing worksheet rows
94%  Saving workbook
97%  Replacing previous export file
98%  Opening exported workbook
100% Export complete
```

The export applies the selected date range to file activity, summaries, errors, user/session activity, and program activity.

## Command-Line Switches

Normal mode:

```powershell
DeskPulse.exe
```

Debug accepted monitored events:

```powershell
DeskPulse.exe -debug
```

Debug accepted and skipped events:

```powershell
DeskPulse.exe -debug -debug-skipped
```

Show hidden Maintenance tab:

```powershell
DeskPulse.exe -maintenance
```

Portable cleanup/uninstall mode:

```powershell
DeskPulse.exe -uninstall
```

The `-uninstall` mode removes current-user registry settings and generated log/report files, but preserves the SQLite database.

## Administrator Requirement

DeskPulse must run as Administrator.

Reason:

```text
Windows kernel ETW file I/O tracing requires elevation.
```

If DeskPulse is not elevated, it should fail with an Administrator/elevation message.


## AI-Assisted Development Note

DeskPulse was developed with AI-assisted coding support.

AI was used to help draft code, explore implementation options, review errors, improve documentation, and speed up iteration. Final decisions, testing, project direction, and release responsibility remain with the project maintainer.

## Ordered Exclusion Rules

Maintenance mode supports ordered exclusion rules. The upper rule wins. This allows broad exclusions with narrow include exceptions above them.

Example:

```text
include|folder|recursive|C:\Program Files\Autodesk\AutoCAD 2027
exclude|folder|recursive|C:\Program Files
```

Folder rules can be Include or Exclude and can apply to subfolders. Existing plain folder/process exclusions remain valid and are treated as exclude rules.


0.1.1 UI note: Maintenance > Logging Rules uses ordered Include/Exclude rules. Rules can be added for folders, exact files, and processes/programs; the upper matching rule wins.

## Maintenance Logging Rules

In maintenance mode, the former Exclusions page is now named `Logging Rules`. It is a single ordered rule manager for folder, exact file, and process/program rules.

Rules are checked from top to bottom; the first matching rule wins. Use Include rules above broad Exclude rules to create exceptions. Folder rules support subfolder matching. Exact file rules match only the selected file path. The rule-entry area includes Browse buttons for folders, exact files, and `.exe` program files. The Statistics grid also has a right-click action to add an exact file exclusion from the Top 100 full paths view.

The right-click tray menu shows `Maintenance...` only when DeskPulse was started with `-maintenance` or `-m`, and it opens Settings directly on the Maintenance tab.


## Maintenance Form

When DeskPulse is started with `-maintenance` or `-m`, the right-click tray menu shows `Maintenance...`. This opens a dedicated Maintenance form. Maintenance is no longer shown inside the normal Settings window.


## Maintenance button tooltips

Maintenance action buttons show hover text explaining exactly what each button affects, especially destructive actions such as clearing records, deleting generated files, removing matching past records, or removing registry settings.

### Maintenance Cleanup clarification

The Maintenance cleanup page separates three different actions:

- `Delete All ...` buttons remove complete categories of stored activity records.
- `Remove Unwanted Data...` removes only past file/program records matching current Exclude rules in Logging Rules.
- Generated-file cleanup deletes only reports/log files, not database records.
