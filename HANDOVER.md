# DeskPulse Handover Document — Version 0.1.3.0

## 1. Project Overview

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected activity on a Windows PC and stores the results in a local SQLite database. Excel is used only for export/reporting.

Current version:

```text
0.1.3.0
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

Release note:

```text
0.1.3.0 is locked as the all-forms designer-editable source baseline. Future feature changes should be tracked as 0.1.4.0 work unless a critical 0.1.3.0 hotfix is required.
```

Important note:

```text
This package was prepared as source/documentation files. It was not compile-tested inside the AI environment because the .NET SDK is not available there. Compile and test locally before committing or publishing.
```

## 2. Files Included in the 0.1.3.0 Package

```text
Program.cs
Forms/*.cs
Forms/*.Designer.cs
Forms/*.resx
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


## Designer/Form Refactor Status

The 0.1.3.0 package contains the all-forms designer-editable UI refactor for the visible WinForms surfaces.

The following forms are now designer-compatible partial classes:

```text
Forms/AboutForm.cs
Forms/AboutForm.Designer.cs
Forms/AboutForm.resx
Forms/ExportDateRangeForm.cs
Forms/ExportDateRangeForm.Designer.cs
Forms/ExportDateRangeForm.resx
Forms/MaintenanceProgressForm.cs
Forms/MaintenanceProgressForm.Designer.cs
Forms/MaintenanceProgressForm.resx
```

The visible Settings and Maintenance layouts are now designer-backed through `Forms/SettingsForm.cs` / `Forms/SettingsForm.Designer.cs`.

```text
Forms/SettingsForm.cs
Forms/SettingsForm.Designer.cs
Forms/MaintenanceForm.cs
```

`MaintenanceForm.cs` remains a thin launcher/wrapper for maintenance mode. The actual editable Maintenance layout is on the Maintenance tab inside `SettingsForm.Designer.cs`.


## Designer Forms Status

Designer-compatible partial forms currently included:

```text
Forms/AboutForm.cs
Forms/AboutForm.Designer.cs
Forms/AboutForm.resx
Forms/ExportDateRangeForm.cs
Forms/ExportDateRangeForm.Designer.cs
Forms/ExportDateRangeForm.resx
Forms/MaintenanceProgressForm.cs
Forms/MaintenanceProgressForm.Designer.cs
Forms/MaintenanceProgressForm.resx
Forms/SettingsForm.cs
Forms/SettingsForm.Designer.cs
Forms/SettingsForm.resx
```

`SettingsForm` is now designer-editable for normal Settings use. The outer window, main tab control, General tab, Files tab, Export Options tab, footer separator, Save button, and Cancel button are in `SettingsForm.Designer.cs`. The dynamic export field sub-tabs are still rebuilt at runtime because they depend on the selected worksheet configuration.

`MaintenanceForm` remains a thin launcher/wrapper around `SettingsForm(true)`; the editable maintenance UI is now located in `SettingsForm.Designer.cs` on the Maintenance tab.

## 3. Version References

Expected current values:

```text
AppInfo.Version = "0.1.3.0"
DeskPulse.csproj Version = 0.1.3.0
DeskPulse.csproj AssemblyVersion = 0.1.3.0
DeskPulse.csproj FileVersion = 0.1.3.0
DeskPulse.csproj InformationalVersion = 0.1.3.0
app.manifest assemblyIdentity version = 0.1.3.0
```

Historical version references may remain in `CHANGELOG.md` because the changelog preserves release history.

## 4. Main 0.1.3.0 Scope

Version `0.1.3.0` includes:

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

Right-click tray icon in normal mode:

```text
About
Exit
```

Right-click tray icon in maintenance mode:

```text
About
Maintenance...
Exit
```

`Maintenance...` opens Settings directly on the Maintenance tab.

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
  --output ".\publish\v0.1.3.0" `
  /p:PublishSingleFile=false
```

Published executable:

```text
publish\v0.1.3.0\DeskPulse.exe
```

Run from Administrator PowerShell:

```powershell
cd ".\publish\v0.1.3.0"
.\DeskPulse.exe
```

Do not copy only the EXE unless single-file publishing has been separately tested. The safer current deployment is the full publish folder.

## Exclusion cleanup of past records

Maintenance > Logging Rules now includes:

```text
Manual folder entry field
Remove Unwanted Data
```

This action permanently deletes existing `ActivityEvents` and `ProgramEvents` rows that match the current current Exclude folder/file/process rules. It does not delete logging rules and does not delete `UserEvents`. It must show a warning dialog before deleting data.

## 18. Testing Checklist for 0.1.3.0

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

- README says current version `0.1.3.0`
- README publish folder uses `v0.1.3.0`
- HANDOVER says current version `0.1.3.0`
- ROADMAP exists and identifies `0.1.3.0` as locked baseline and `0.1.4.0` as the next development version
- CHANGELOG has a current `0.1.3.0` section and preserves historical entries

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
0.1.4.0 — Stabilisation, UI polish, and export improvements
```

Main planned topics:

- compile-test and runtime-test the locked 0.1.3.0 package locally
- tidy remaining form spacing and reduce unnecessary whitespace
- improve resizing/anchoring of tables and path columns
- add export cancellation and export preview/summary
- improve export performance on large date ranges
- refine Maintenance wording if testing shows ambiguity

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
Release DeskPulse v0.1.3.0
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
9. Keep Maintenance hidden unless launched with `-maintenance` or `-m`.
10. Keep `-debug`/`-d`, `-maintenance`/`-m`, and `-uninstall`/`-u` as separate concepts.
11. Preserve the database when using `-uninstall`.
12. Treat the app as a portable pre-release until a formal installer exists.
13. Do not assume copying only the EXE is valid.
14. Keep normal successful actions quiet; only errors should show popups.


## 0.1.3.0 Maintenance Implementation Notes

Version `0.1.3.0` adds a hidden Maintenance workspace that is visible only when DeskPulse is started with `-maintenance` or `-m`.

Maintenance sub-tabs:

```text
Database
Statistics
Cleanup
Logging Rules
Diagnostics
```

Key implementation points:

- `AppRuntime.MaintenanceModeEnabled` now accepts `maintenance` and `m` switch names.
- `AppSettings` now stores ordered `LoggingRules` in the current-user registry while preserving legacy `ExcludedFolders` and `ExcludedProcesses` compatibility values.
- Maintenance > Logging Rules includes a compact rule-entry panel with Browse support for folder/file/program rules.
- File logging checks ordered Logging Rules before writing file activity records.
- Program activity logging checks ordered Logging Rules before writing program start/stop records.
- `DeskPulseDatabase` now provides maintenance overview, Top 100 statistics, and clear-record methods.
- Database clear actions delete records but keep the SQLite database structure.
- Cleanup actions for generated files do not delete the SQLite database.

Compile/test checklist additions:

- Start normally and confirm Maintenance is hidden.
- Start with `-maintenance` and confirm Maintenance is visible.
- Start with `-m` and confirm Maintenance is visible.
- Confirm Maintenance > Database loads without crashing on an empty or existing database.
- Confirm Maintenance > Statistics shows empty grids gracefully if there are no records.
- Confirm clear buttons require confirmation.
- Confirm the Logging Rules add-rule panel can add Folder, File, and Process/Program rules.
- Confirm Logging Rules save to `HKCU\Software\DeskPulse`.
- Confirm Exclude rules reduce future logged noise and Include rules override lower Exclude rules.


## Folder Exclusion Subfolder Option

Maintenance > Cleanup has a progress dialog for `Remove Unwanted Data`. It starts at 1% while matching records are determined, then advances the remaining 99% according to actual deleted records.

Maintenance > Logging Rules supports a subfolder option for folder rules. When checked, the saved folder rule receives the `|recursive` suffix. When unchecked, the saved folder rule receives the `|folder-only` suffix. Existing plain folder rules remain recursive so older settings keep their previous behaviour.

Examples:

```text
C:\Users\eysseleink\AppData|recursive
C:\Some\Exact\Folder|folder-only
```

The exclusion matcher must compare folder boundaries, not simple string prefixes, so `C:\Users\Kai\AppData` must not match `C:\Users\Kai\AppDataBackup`.


## 0.1.3.0 Exclusion Rule Ordering Update

The Maintenance > Logging Rules page supports ordered rules. Rules are evaluated from top to bottom and the first matching rule wins. This permits patterns such as including a specific application folder above a broad Program Files exclusion.

Folder rule format:

```text
include|folder|recursive|C:\Specific\Allowed\Folder
exclude|folder|recursive|C:\Program Files
exclude|folder|folder-only|C:\Specific\Folder
```

Process rule format:

```text
include|process||acad.exe
exclude|process||somebackgroundprocess.exe
```

Legacy plain exclusions remain supported and are interpreted as exclude rules.


## Logging Rules redesign

The Maintenance sub-tab formerly called Exclusions is now `Logging Rules`. It uses one ordered rule list for folder, exact file, and process/program rules. Rules are evaluated from top to bottom; the first matching rule wins.

Rule storage now includes the `LoggingRules` registry value. `ExcludedFolders` and `ExcludedProcesses` remain populated for compatibility, but the application runtime checks the ordered `LoggingRules` list.

The UI has an add-rule panel with Folder/Process selection, Exclude/Include selection, subfolder option for folder rules, and Browse buttons. The rules table supports Move Up, Move Down, Remove, Duplicate, and Reset Defaults.


## 0.1.3.0 UI correction — Maintenance form separation

Maintenance is now a dedicated right-click form when DeskPulse is started with `-maintenance` or `-m`. The normal Settings form must contain only General, Files, and Export Options. Do not re-add Maintenance as a normal Settings tab.


0.1.3.0 note: Logging Rules now support exact file exclusions. In Maintenance > Statistics, right-click a row in Top 100 full paths to add an exact file exclusion for that specific file.


## Maintenance button tooltips

Maintenance action buttons use WinForms tooltips to describe the scope of each action before the user clicks it. Destructive actions should clearly say whether they affect selected records, matching excluded records, generated files, registry settings, or all activity records.

## Maintenance cleanup UI update

The Cleanup tab now separates destructive record clearing from rule-based unwanted-data cleanup. The `Delete All ...` buttons clear whole activity categories. The `Remove Unwanted Data...` button applies current Exclude logging rules to past file/program records only, with warning/progress. Generated-file cleanup remains separate.


## 0.1.3.0 Final Lock Notes

- Cleanup tab order is safety-first: generated files first, rule-based unwanted-data cleanup second, and full record deletion last.
- Statistics supports multi-select with Shift/Ctrl and applies add-rule actions to all selected rows.
- Logging Rules support Folder, File, and Process/Program rules.
- 0.1.3.0 is locked as the all-forms designer-editable source baseline. Future changes should move to 0.1.4.0 unless a critical 0.1.3.0 hotfix is required.
