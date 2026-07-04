# DeskPulse Handover Document — Version 0.1.0

## 1. Project Overview

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected activity on a Windows PC and stores the results in a local SQLite database. Excel is used only for export/reporting.

Current version:

```text
0.1.0
```

Repository:

```text
https://github.com/KaiEysselein/DeskPulse
```

Maintainer:

```text
Kai Eysselein
```

Development status:

```text
Early pre-release / working baseline
```

Important note:

```text
This package was prepared as source/documentation files. It was not compile-tested inside the AI environment because the .NET SDK is not available there. Compile and test locally before committing or publishing.
```

## 2. Files Included in the 0.1.0 Package

```text
Program.cs
DeskPulse.csproj
app.manifest
README.md
CHANGELOG.md
HANDOVER.md
ROADMAP.md
```

Expected repository files that may also already exist locally:

```text
file-logger.ico
LICENSE
.gitignore
```

The project file references `file-logger.ico`. If that file is not already in the repository, add it before building.

## 3. Version References

Expected current values:

```text
AppInfo.Version = "0.1.0"
DeskPulse.csproj Version = 0.1.0
DeskPulse.csproj AssemblyVersion = 0.1.0.0
DeskPulse.csproj FileVersion = 0.1.0.0
DeskPulse.csproj InformationalVersion = 0.1.0
app.manifest assemblyIdentity version = 0.1.0.0
```

Historical version references may remain in `CHANGELOG.md` because the changelog preserves release history.

## 4. Main 0.1.0 Scope

Version `0.1.0` includes:

- quiet startup with no normal success balloon
- no `Settings saved` balloon
- polished WinForms UI layout
- `General` settings tab
- `Start DeskPulse when I log in to Windows` option
- Task Scheduler task creation/removal for startup at user logon
- `Log program start and close activity` option
- program start/close logging for the current interactive Windows session
- `Programs` database/export concept
- left-click tray menu for day-to-day actions
- right-click tray menu for About/Exit
- export date-range form with two calendar controls only
- `Export Activity Log` menu text
- real percentage export progress based on row writing
- status text below the export progress bar
- `ROADMAP.md` for planned future work

## 5. Tray Menu Behaviour

Left-click tray icon:

```text
Export Activity Log
Settings...
```

Right-click tray icon:

```text
About
Exit
```

No normal success balloon notifications should appear.

Error dialogs should still appear when startup, settings, export, or monitoring fails.

## 6. Settings Window

Visible normal tabs:

```text
General
Files
Export Options
```

Hidden tab:

```text
Maintenance
```

The Maintenance tab is visible only when launched with:

```powershell
DeskPulse.exe -maintenance
```

## 7. General Settings

The General tab contains:

```text
Start DeskPulse when I log in to Windows
Log program start and close activity
```

The startup option uses Windows Task Scheduler rather than the Startup folder because DeskPulse requires Administrator privileges for ETW tracing.

Task name:

```text
DeskPulse
```

Trigger:

```text
ONLOGON
```

Run level:

```text
Highest privileges
```

Important testing note:

```text
Enable this option from the final/published folder path because Task Scheduler points to the exact DeskPulse.exe path used when the setting is saved.
```

## 8. File Monitoring

DeskPulse uses Windows ETW file I/O tracing.

It logs selected extensions only. The monitored file-type list in the Files tab is the source of truth.

The app must continue excluding:

- DeskPulse process activity
- `DeskPulse.db`
- `DeskPulse-export.xlsx`
- temporary folders when `IgnoreTempFolders` is enabled

## 9. Program Activity Logging

When enabled, DeskPulse logs program/process start and close events for the current interactive Windows session.

Important behaviour:

- running processes at DeskPulse startup are treated as baseline
- baseline processes are not logged as newly started
- DeskPulse excludes itself
- this is not a Windows service or full-machine audit log

Database table:

```text
ProgramEvents
```

Export worksheet:

```text
Programs
```

Common fields:

```text
Date
Time
Event Type
Event
Program
Process ID
Program Path
Window Title
User
Computer
App Version
Note
```

## 10. User / Session Activity Logging

DeskPulse logs user/session activity while the tray app is running.

Examples:

```text
DeskPulse started
DeskPulse stopped
PC locked
PC unlocked
User logged on
User logging off
Console session connected
Console session disconnected
Remote session connected
Remote session disconnected
```

Implementation uses:

```csharp
Microsoft.Win32.SystemEvents.SessionSwitch
```

This is current-user/session based, not a service-level global machine audit.

## 11. Database Design

Live database:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Main tables:

```text
ActivityEvents
UserEvents
ProgramEvents
```

Excel is export/reporting only.

Do not reintroduce CSV as live storage.

## 12. Excel Export

Menu text:

```text
Export Activity Log
```

Default export date range:

```text
Today only
```

Date range UI:

```text
Two calendar controls only, no date text fields
```

Progress bar:

```text
Determinate percentage progress based on rows written
```

Progress status text below the bar:

```text
1%   Reading activity records
3%   Counting rows to export
5-93% Writing worksheet rows
94%  Saving workbook
97%  Replacing previous export file
98%  Opening exported workbook
100% Export complete
```

Available export worksheets:

```text
File Activity
Daily Summary
Summary by Extension
Summary by Process
Errors
User
Programs
```

The selected date range applies to all export worksheets.

## 13. Registry Settings

Settings path:

```text
HKCU\Software\DeskPulse
```

Important values:

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

## 14. Command-Line Switches

Normal mode:

```powershell
DeskPulse.exe
```

Debug mode:

```powershell
DeskPulse.exe -debug
```

Debug skipped events:

```powershell
DeskPulse.exe -debug -debug-skipped
```

Maintenance mode:

```powershell
DeskPulse.exe -maintenance
```

Portable cleanup/uninstall mode:

```powershell
DeskPulse.exe -uninstall
```

Accepted switch prefixes:

```text
-
--
/
```

## 15. Build Instructions

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

## 16. Run From Source

Run from an Administrator PowerShell or Administrator VS Code terminal:

```powershell
dotnet run
```

Run from source with Maintenance visible:

```powershell
dotnet run -- -maintenance
```

Run from source with debug logging:

```powershell
dotnet run -- -debug
```

## 17. Publish Instructions

Recommended publish method remains a portable folder publish.

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

## 18. Testing Checklist for 0.1.0

Build/startup:

- `dotnet clean` succeeds
- `dotnet restore` succeeds
- `dotnet build` succeeds
- app runs as Administrator
- tray icon appears
- normal startup shows no success balloon
- settings save shows no success balloon
- `-maintenance` shows the Maintenance tab
- `-debug` enables diagnostics without showing Maintenance by itself
- `-uninstall` performs cleanup and exits

Tray menus:

- left-click shows Export Activity Log and Settings
- right-click shows About and Exit

General settings:

- startup checkbox creates/removes Task Scheduler task
- program activity checkbox persists in registry
- program activity logs start and close events when enabled

File monitoring:

- default monitored extensions load
- added monitored extensions persist
- `.dwg`, `.zip`, and `.pad` monitoring still works
- temporary-folder exclusion works
- DeskPulse does not log its own database/export files

Export:

- Export Activity Log opens calendar-only date-range dialog
- both calendars default to today
- selected date range filters all worksheets
- progress bar shows percentage progress
- status text appears below progress bar
- form closes only after export/open completes
- Programs worksheet appears when selected

Documentation:

- README says current version `0.1.0`
- README publish folder uses `v0.1.0`
- HANDOVER says current version `0.1.0`
- ROADMAP exists and starts with `0.1.1` future work
- CHANGELOG has a current `0.1.0` section and preserves historical entries

## 19. Known Limitations / Items to Watch

- The package was not compile-tested in the AI environment.
- DeskPulse is still an early pre-release.
- No formal installer exists yet.
- The app must run as Administrator for ETW tracing.
- Program activity logging is current-session based, not service/global-machine based.
- User/session activity logging is tray-app/session based, not service/global-machine based.
- Complex app save behaviour may produce multiple file activity rows.
- Some Windows/system background files may still be noisy and need exclusion filters in a future version.
- Portable folder publish remains safer than copying only the EXE.

## 20. Roadmap / Future Work

Future work is documented in:

```text
ROADMAP.md
```

Likely next version:

```text
0.1.1 — Data Management and Logging Filters
```

Main planned topics:

- file/database size limits
- manual clear database option
- clear generated reports/logs option
- exclude noisy Windows/system paths
- exclude noisy processes
- better filtering for program-start background file activity

## 21. GitHub / Commit Notes

Files suitable for commit:

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

Suggested commit message:

```text
Release DeskPulse v0.1.0
```

## 22. Instructions for Future AI Assistance

When continuing this project:

1. Provide full replacement files when code changes are requested.
2. Preserve SQLite as the live store.
3. Preserve Excel as export/reporting only.
4. Keep `File Activity` as the worksheet name for file activity exports.
5. Keep `Extension` / `File Type` as a separate field.
6. Keep the path split into `Full Path`, `Folder Path`, `File Name`, and `Extension`.
7. Keep DeskPulse self-exclusion.
8. Keep database/export file exclusion.
9. Keep Maintenance hidden unless launched with `-maintenance`.
10. Keep `-debug`, `-maintenance`, and `-uninstall` as separate concepts.
11. Preserve the database when using `-uninstall`.
12. Treat the app as a portable pre-release until a formal installer exists.
13. Do not assume copying only the EXE is valid.
14. Keep normal successful actions quiet; only errors should show popups.
