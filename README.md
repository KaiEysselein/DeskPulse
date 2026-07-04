# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It quietly tracks selected activity on a Windows PC and stores the records in a local SQLite database. Excel is used only for exports and reports; it is not the live log file.

## Version

Current version: `0.1.0`

## What DeskPulse Logs

DeskPulse can log:

- selected file open/write/close activity
- DeskPulse start/stop activity
- PC lock/unlock and Windows session activity
- program start/close activity in the current interactive Windows session

Email read/sent logging is not included in version `0.1.0`.

## Main Features

- Windows tray application
- quiet normal startup with no success popups
- left-click tray menu for daily actions:
  - `Export Activity Log`
  - `Settings...`
- right-click tray menu for secondary actions:
  - `About`
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
- hidden Maintenance tab available only with `-maintenance`
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

Also accepted:

```text
--maintenance
/maintenance
```

The Maintenance tab is for portable-app administration and cleanup. It includes tools such as:

- open the DeskPulse data folder
- open the DeskPulse program folder
- open the diagnostic log
- show active monitored extensions
- remove current-user registry settings

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

## Build Instructions

From the repository folder:

```powershell
dotnet clean
dotnet restore
dotnet build
```

Expected result:

```text
Build succeeded.
```

## Run From Source

Run from Administrator PowerShell or Administrator VS Code terminal:

```powershell
dotnet run
```

Run with hidden maintenance tools enabled:

```powershell
dotnet run -- -maintenance
```

Run with debug logging enabled:

```powershell
dotnet run -- -debug
```

## Publish Instructions

Recommended current publish method is a portable folder publish.

```powershell
dotnet publish .\DeskPulse.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output ".\publish\v0.1.0" `
  /p:PublishSingleFile=false
```

Published executable:

```text
publish\v0.1.0\DeskPulse.exe
```

Run from Administrator PowerShell:

```powershell
cd ".\publish\v0.1.0"
.\DeskPulse.exe
```

Do not copy only the EXE unless single-file publishing has been separately tested. The safer current deployment is the full publish folder.

## Git Hygiene

Source and documentation files that may be committed:

```text
Program.cs
DeskPulse.csproj
README.md
CHANGELOG.md
HANDOVER.md
ROADMAP.md
app.manifest
file-logger.ico
LICENSE
```

Do not commit generated/runtime/build files:

```text
bin/
obj/
publish/
*.db
*.db-shm
*.db-wal
*.xlsx
*.csv
*.user
.vs/
```

Suggested `.gitignore` entries:

```gitignore
bin/
obj/
publish/

*.db
*.db-shm
*.db-wal
*.xlsx
*.csv
*.user

.vs/
```

## AI-Assisted Development Note

DeskPulse was developed with AI-assisted coding support.

AI was used to help draft code, explore implementation options, review errors, improve documentation, and speed up iteration. Final decisions, testing, project direction, and release responsibility remain with the project maintainer.
