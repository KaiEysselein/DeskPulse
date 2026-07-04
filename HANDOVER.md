# DeskPulse Handover Document — Version 0.1.0

## 1. Project Overview

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected file activity on a Windows machine using Windows ETW file I/O tracing and stores the results in a local SQLite database. Excel is not the live log file; Excel is used only for export/reporting.

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
This 0.1.0 package was prepared as source/documentation files. It was not compile-tested inside the AI environment because dotnet/.NET SDK was not available there. Compile and test locally before committing or publishing.
```

---

## 2. Version 0.1.0 Summary

Version 0.1.0 continues from the 0.0.4 baseline with:

- application/project/manifest version references updated to `0.1.0`
- successful-startup tray balloon removed so DeskPulse loads quietly to the tray
- startup error messages preserved
- new normal `General` settings tab
- `Start DeskPulse when I log in to Windows` option
- current-user Windows Task Scheduler autostart integration
- registry-backed `StartWithWindows` setting
- Maintenance tab startup placeholder replaced with a pointer to the General tab
- settings window visually polished with grouped sections, better spacing, larger layout, consistent controls, and clearer help text
- About window visually polished with cleaner title, version, description, and project link layout
- program start/close activity logging for programs in the current interactive Windows session
- new `Programs` Excel export worksheet
- registry-backed `LogProgramActivity` setting

The main live storage remains SQLite. Excel remains export/reporting only.

### 2.1 UI polish in this 0.1.0 package

The WinForms settings UI was revised to look less like a prototype:

```text
- larger fixed dialog with better margins
- Segoe UI as the form font
- grouped sections using GroupBox controls
- cleaner General, Files, Export Options, and Maintenance tab layouts
- consistent action button sizing
- less cramped export-options layout
- cleaner About dialog with separate title and version labels
```

This is a visual/layout cleanup only. It does not intentionally change the monitoring, database, export, startup-task, or maintenance behaviour.

### Tray menu behaviour

Tray icon actions are split by mouse button.

Left-click opens the day-to-day action menu:

```text
Export Activity Log
Settings...
```

Right-click opens the secondary/system menu:

```text
About
Exit
```


### Program activity logging

0.1.0 adds optional program start/close logging.

Setting:

```text
General > Log program start and close activity
```

Registry value:

```text
HKCU\Software\DeskPulse\LogProgramActivity
```

Default:

```text
Enabled
```

Implementation summary:

```text
ProgramActivityMonitor periodically snapshots processes in the current interactive Windows session.
Existing processes are captured as a baseline on monitor startup and are not logged as newly started.
New process IDs are logged as ProgramStarted.
Missing process IDs are logged as ProgramStopped.
DeskPulse itself is excluded.
```

Database table:

```text
ProgramEvents
```

Important limitation:

```text
This is user-session process/activity logging, not a full Windows service or machine-wide audit log.
It does not monitor all users globally, and some protected/system process details may be unavailable.
```

Excel export worksheet:

```text
Programs
```

Typical fields:

```text
Id
Created At
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

---

## 3. Files Included in the 0.1.0 Package

The current 0.1.0 package contains:

```text
Program.cs
DeskPulse.csproj
app.manifest
CHANGELOG.md
HANDOVER.md
```

The package file is:

```text
DeskPulse-0.1.0-export-activity-log-percent-progress.zip
```

Important:

```text
Use the files from the latest ZIP package as the source of truth for this handover.
```

---


### Export Activity Log dialog update

The tray menu item is labelled `Export Activity Log`. The export dialog uses two calendar controls only, defaults both to today, shows a real percentage progress bar while exporting, and closes only after the export/open operation completes.

## 4. Current Version References

Version references were updated to 0.1.0 in:

```text
Program.cs
DeskPulse.csproj
app.manifest
CHANGELOG.md
HANDOVER.md
```

Expected values:

```text
AppInfo.Version = "0.1.0"
DeskPulse.csproj Version = 0.1.0
DeskPulse.csproj AssemblyVersion = 0.1.0.0
DeskPulse.csproj FileVersion = 0.1.0.0
DeskPulse.csproj InformationalVersion = 0.1.0
```

Historical `0.0.4`, `0.0.3`, `0.0.2`, and `0.0.1` references should remain in the changelog history.

---

## 5. Main Design Decisions Preserved

### 5.1 SQLite remains the live data store

The live database remains:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Do not reintroduce CSV as the live storage mechanism.

### 5.2 Excel remains export/reporting only

The Excel export remains:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx
```

The tray menu item remains:

```text
Export Activity Log
```

This opens an export-options dialog first, defaulted to today only. After the user selects a start and end date, DeskPulse exports matching SQLite rows to Excel, updates a real percentage progress bar while rows are written, and opens the exported workbook. The date range is inclusive.


### 5.2.1 Excel export date range

When the user clicks:

```text
Export Activity Log
```

DeskPulse shows an `Export Activity Log` dialog with:

```text
Start date
End date
```

Both dates default to the current day. The export is then restricted to rows whose activity/user event date falls within the selected inclusive date range. This applies to the detailed File Activity sheet and to derived summary/error/user worksheets.

### 5.2.2 Excel export percentage progress

The export dialog now uses a determinate progress bar rather than a marquee/activity indicator. The current progress milestone is shown as text below the progress bar.

Progress is calculated from the number of data rows that will be written across the selected export worksheets. DeskPulse first reads and filters the SQLite rows for the selected date range, counts the rows that each selected worksheet will write, and then reports progress as rows are written.

The status line below the progress bar should follow this progress flow:

```text
1%   Reading activity records
3%   Counting rows to export
5-93% Writing worksheet rows
94%  Saving workbook
97%  Replacing previous export file
98%  Opening exported workbook
100% Export complete
```

Important implementation classes/methods:

```text
ExportProgressInfo
ExcelExportProgressTracker
DeskPulseDatabase.CountRowsToWrite(...)
DeskPulseDatabase.ExportToExcel(..., IProgress<ExportProgressInfo>?)
ExportDateRangeForm.UpdateExportProgress(...)
```

For empty exports, the total row count is treated as at least one so the progress bar can still advance cleanly to completion.

### 5.3 DeskPulse must not log its own file activity

The app must continue excluding:

- the DeskPulse process itself
- `DeskPulse.db`
- `DeskPulse-export.xlsx`
- temp folders when the setting is enabled

### 5.4 File extension remains its own export field

The file extension must remain separately available as:

```text
Extension
```

The user-facing label in the export options can be more understandable:

```text
File Type / Extension
```

### 5.5 Full path is still split

File paths must remain split into:

```text
Full Path
Folder Path
File Name
Extension
```

---

## 6. Command-Line Switches

DeskPulse 0.0.4 supports these command-line switches.

### 6.1 Normal mode

```powershell
DeskPulse.exe
```

Behavior:

- starts tray app
- shows normal settings tabs
- hides Maintenance tab
- starts monitoring if running as Administrator

### 6.2 Debug mode

```powershell
DeskPulse.exe -debug
```

Also accepted:

```text
--debug
/debug
```

Behavior:

- enables accepted-event diagnostic logging
- does not automatically show Maintenance tab

Diagnostic log:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-diagnostics.log
```

### 6.3 Full skipped-event debug mode

```powershell
DeskPulse.exe -debug -debug-skipped
```

Also accepted:

```text
--debug-skipped
/debug-skipped
```

Behavior:

- logs accepted monitored events
- also logs skipped ETW events and skip reasons
- can grow very quickly
- use only for short troubleshooting runs

### 6.4 Maintenance mode

```powershell
DeskPulse.exe -maintenance
```

Also accepted:

```text
--maintenance
/maintenance
```

Behavior:

- starts normal tray app
- makes the hidden `Maintenance` settings tab visible
- does not automatically enable diagnostic logging

Reason:

```text
Maintenance tools should only be available when deliberately started from Command Prompt / PowerShell, so ordinary users do not easily access cleanup/admin functions.
```

### 6.5 Portable cleanup / uninstall mode

```powershell
DeskPulse.exe -uninstall
```

Also accepted:

```text
--uninstall
/uninstall
```

Behavior:

- runs portable cleanup
- exits without starting tray monitoring

The intended cleanup is conservative:

Deletes/clears:

```text
HKCU\Software\DeskPulse
%TEMP%\DeskPulse-startup.log
%USERPROFILE%\Documents\DeskPulse\DeskPulse-diagnostics.log
%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx
```

Keeps:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Meaning:

```text
Settings, diagnostics, and generated reports are removed. The actual SQLite activity data remains.
```

---

## 7. Settings Window Structure

The 0.1.0 settings window has these tabs:

```text
General
Files
Export Options
Maintenance  [hidden unless started with -maintenance]
```

### 7.1 General tab

Purpose:

```text
Contains ordinary application-wide settings that are not specifically file monitoring, export layout, or hidden maintenance tools.
```

Controls:

- `Start DeskPulse when I log in to Windows`

Implementation:

```text
Uses Windows Task Scheduler, not the Startup folder.
Task name: DeskPulse
Trigger: ONLOGON
Run level: HIGHEST
Command source: Application.ExecutablePath
```

Reason:

```text
DeskPulse requires Administrator privileges for ETW tracing. A simple Startup folder shortcut is not sufficient for reliable elevated startup.
```

Important deployment note:

```text
Enable this setting only after DeskPulse is running from the folder that should remain on the user's computer. The scheduled task points to the executable path used at the time the setting is saved.
```

### 7.2 Files tab

Controls:

- data folder path
- temporary-folder exclusion
- registered Windows file types
- monitored file type list

The right-hand monitored file type list remains the source of truth.

### 7.3 Export Options tab

Purpose:

```text
Controls which worksheets and columns are created in the Excel export.
```

Behavior:

- user can tick/select which worksheets are created
- date range is selected at export time, not in this settings tab
- user can sort selected worksheet order with Up/Down buttons
- each selected worksheet has a field sub-tab
- each worksheet field sub-tab lists available fields/columns
- fields can be checked/unchecked
- field order can be sorted

The default remains:

```text
File Activity only, with standard/default fields enabled
```

### 7.4 Maintenance tab

Hidden during normal use.

Visible only when launched with:

```powershell
DeskPulse.exe -maintenance
```

Controls include:

- open data folder
- open program folder
- open diagnostic log
- show active monitored extensions
- remove registry settings
- note that startup settings are now available on the normal General tab

---

## 8. Export Worksheets

0.1.0 supports the following export worksheet options:

```text
File Activity
Daily Summary
Summary by Extension
Summary by Process
Errors
User
```

### 8.1 File Activity

Purpose:

```text
Detailed file open/write/close activity.
```

Available fields include:

```text
Id
Created At
Activity Type
Full Path
Folder Path
File Name
File Type / Extension
Date Opened
Time Opened
Size At Opening
First Write Date
First Write Time
Last Write Date
Last Write Time
Write Count
Size At Last Write
Date Closed
Time Closed
Size At Closing
Inferred Action
Process
Process ID
Note
```

Worksheet name:

```text
File Activity
```

Important:

```text
Keep this worksheet name exactly as File Activity unless the user explicitly changes the design.
```

### 8.2 Daily Summary

Purpose:

```text
Summarizes activity per day.
```

Available fields include:

```text
Date
Events
Open Events
Write Events
Close Events
Error Events
Distinct Files
Distinct Processes
```

### 8.3 Summary by Extension

Purpose:

```text
Summarizes file activity by file type/extension.
```

Available fields include:

```text
File Type / Extension
Events
Distinct Files
Write Events
Last Event
```

### 8.4 Summary by Process

Purpose:

```text
Summarizes file activity by process/application.
```

Available fields include:

```text
Process
Process ID
Events
Distinct Files
Write Events
Last Event
```

### 8.5 Errors

Purpose:

```text
Exports DeskPulse monitor/application error records from the activity table.
```

Available fields include:

```text
Id
Created At
Item / Full Path
Process
Process ID
Note
```

### 8.6 User

Purpose:

```text
Exports DeskPulse start/stop and Windows user/session activity.
```

Available fields include:

```text
Id
Created At
Date
Time
Event Type
Event
User
Computer
Process
Process ID
App Version
Note
```

This worksheet should be labelled exactly:

```text
User
```

---

## 9. User / Session Activity Logging

0.0.4 adds a new user/session activity logging concept.

The app logs:

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

Implementation approach:

```csharp
Microsoft.Win32.SystemEvents.SessionSwitch
```

Key session switch reasons handled:

```text
SessionLock
SessionUnlock
SessionLogon
SessionLogoff
ConsoleConnect
ConsoleDisconnect
RemoteConnect
RemoteDisconnect
```

Important limitation:

```text
DeskPulse is a tray app. It logs session activity while it is running in the user's Windows session. It is not a Windows service and does not monitor all users on the machine globally.
```

---

## 10. Database Design

### 10.1 Existing ActivityEvents table

The main file activity table remains:

```text
ActivityEvents
```

Key fields:

```text
Id
CreatedAt
ActivityType
Item
FullPath
FolderPath
FileName
Extension
DateOpened
TimeOpened
SizeAtOpening
FirstWriteDate
FirstWriteTime
LastWriteDate
LastWriteTime
WriteCount
SizeAtLastWrite
DateClosed
TimeClosed
SizeAtClosing
InferredAction
ProcessName
ProcessId
Note
```

Important:

```text
Item is retained as a legacy/internal compatibility field. Do not remove it casually.
```

### 10.2 New UserEvents table

0.0.4 adds:

```text
UserEvents
```

Fields:

```text
Id
CreatedAt
EventDate
EventTime
EventType
EventDescription
UserName
MachineName
ProcessName
ProcessId
AppVersion
Note
```

Indexes:

```text
IX_UserEvents_CreatedAt
IX_UserEvents_EventType
```

---

## 11. Registry Settings

DeskPulse stores settings here:

```text
HKCU\Software\DeskPulse
```

Important registry values:

```text
AppVersion
DataFolderPath
DatabaseFilePath
ExcelExportFilePath
ExtensionsToMonitor
ExportSheets
IgnoreTempFolders
StartWithWindows
```

### 11.1 ExportSheets

0.0.4 adds/uses:

```text
ExportSheets
```

Purpose:

```text
Stores selected export worksheets, worksheet order, and selected/sorted fields per worksheet.
```

Default:

```text
File Activity only
```

---

## 12. About Window Text

The About window was intentionally made less technical.

Preferred wording:

```text
DeskPulse quietly tracks selected file activity while you work.

It helps you review what was opened, changed, or saved, and can export clear reports to Excel whenever needed.
```

Avoid making the About window sound like developer documentation. Do not mention ETW, SQLite, or XLSX in the main description unless specifically requested.

---

## 13. Build Instructions

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

If there are errors, fix from the top of the build output first.

---

## 14. Run From Source

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

---

## 15. Publish Instructions

Recommended publish method remains a portable folder publish.

```powershell
dotnet publish .\DeskPulse.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output ".\publish\v0.0.4" `
  /p:PublishSingleFile=false
```

Published executable:

```text
publish\v0.0.4\DeskPulse.exe
```

Run from Administrator PowerShell:

```powershell
cd ".\publish\v0.0.4"
.\DeskPulse.exe
```

Run with hidden Maintenance tab:

```powershell
.\DeskPulse.exe -maintenance
```

Run portable cleanup:

```powershell
.\DeskPulse.exe -uninstall
```

Important:

```text
Do not copy only the EXE unless single-file publishing has been separately tested. The safer current deployment is the full publish folder.
```

---

## 16. Testing Checklist for 0.0.4

### 16.1 Build and startup

- `dotnet clean` succeeds
- `dotnet restore` succeeds
- `dotnet build` succeeds
- published folder builds
- app runs as Administrator
- tray icon appears
- no startup balloon/popup is shown after successful monitoring startup
- startup failure/error messages still appear if monitoring cannot start
- normal startup does not show Maintenance tab
- `-maintenance` startup shows Maintenance tab
- `-debug` enables diagnostics without necessarily showing Maintenance tab
- `-uninstall` performs cleanup and exits

### 16.2 File monitoring

- default monitored extensions still load
- added monitored extensions persist in registry
- `.dwg` logging still works
- `.zip` and `.pad` logging still work after the 0.0.3 immediate OPEN/WRITE fix
- temporary-folder exclusion still works
- DeskPulse does not log its own database/export files
- network/LanmanRedirector path normalization still works acceptably

### 16.3 Export Options

- Settings opens without error
- Export Options tab is visible in normal mode
- worksheet selection works
- worksheet sorting works
- field sub-tabs appear for selected worksheets
- field check/uncheck works
- field sorting works
- export date dialog opens when clicking `Export Activity Log`
- export progress bar shows percentage progress rather than marquee/activity indication
- default export start/end date is today
- export validates that the end date is not before the start date
- export creates only selected worksheets
- export worksheet order follows configured order
- export columns follow configured field order
- default/reset returns to File Activity only

### 16.4 User worksheet

- DeskPulse start event is logged
- DeskPulse stop event is logged on clean exit
- PC lock event is logged
- PC unlock event is logged
- session logoff/logon behavior is checked realistically
- User worksheet appears when selected
- User worksheet exports expected fields

### 16.5 Maintenance

- Maintenance tab hidden in normal startup
- Maintenance tab visible with `-maintenance`
- Open data folder works
- Open program folder works
- Open diagnostic log works
- Show active extensions works
- remove registry settings works after confirmation
- registry cleanup does not delete the database

### 16.6 Documentation

- README says current version `0.0.4`
- README publish folder uses `v0.0.4`
- CHANGELOG has a `0.0.4` section
- historical changelog entries remain intact
- AI-assisted development note remains present

---

## 17. Known Limitations / Items to Watch

- The 0.0.4 package was not compile-tested in the AI environment.
- DeskPulse is still an early pre-release.
- No formal installer exists yet.
- Windows Task Scheduler autostart is still a placeholder and has not been implemented.
- The app must run as Administrator for ETW tracing.
- User/session activity logging is tray-app/session based, not service/global-machine based.
- Some session/logoff events may depend on Windows shutdown/logoff timing.
- Complex app save behavior may produce multiple file activity rows.
- Complex network paths may still need more normalization later.
- Portable folder publish remains safer than copying only the EXE.

Minor documentation cleanup to consider:

```text
README may contain a duplicate `User` bullet in the Export Options worksheet list. Remove the duplicate if present before final commit.
```

---

## 18. GitHub / Commit Notes

Files suitable for commit:

```text
Program.cs
DeskPulse.csproj
app.manifest
CHANGELOG.md
HANDOVER.md
HANDOVER.md
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

Suggested commit message:

```text
Release DeskPulse v0.1.0 general startup settings
```

Alternative shorter commit message:

```text
Add DeskPulse v0.1.0 startup option
```

---

## 19. Suggested Next Iteration

The next sensible version could continue as:

```text
DeskPulse 0.1.0 refinement
```

Recommended focus:

```text
Compile and test the new General tab and Windows startup task creation locally.
```

Expected test scope:

- Settings opens with `General`, `Files`, and `Export Options` tabs in normal mode
- `Start DeskPulse when I log in to Windows` creates a `DeskPulse` Task Scheduler task
- task uses the published executable path
- task runs with highest privileges
- unticking the setting removes the task
- DeskPulse starts quietly in the tray at logon
- startup error messages still appear if monitoring cannot start

---

## 20. Instructions for Future AI Assistance

When continuing this project with an AI assistant:

1. Provide full replacement files when code changes are requested.
2. Do not provide only tiny snippets unless the user explicitly asks for snippets.
3. Preserve SQLite as the live store.
4. Preserve Excel as export/reporting only.
5. Keep `File Activity` as the worksheet name for file activity exports.
6. Keep `Extension` / `File Type` as a separate field.
7. Keep the path split into `Full Path`, `Folder Path`, `File Name`, and `Extension`.
8. Keep DeskPulse self-exclusion.
9. Keep database/export file exclusion.
10. Keep Maintenance hidden unless launched with `-maintenance`.
11. Keep `-debug` and `-maintenance` as separate concepts.
12. Preserve the database when using `-uninstall`.
13. Treat the app as a portable pre-release until a formal installer exists.
14. Do not assume copying only the EXE is valid.
15. Prefer portable publish folder for release builds.
16. Remember that the user prefers full copy/paste code files.
17. Keep About text user-friendly, not overly technical.
18. Autostart uses Windows Task Scheduler, not the Startup folder.

---

## 21. License

License intention:

```text
GNU General Public License v3.0 or later
```

SPDX identifier:

```text
GPL-3.0-or-later
```

---

## 0.1.0 Baseline Note

This package starts DeskPulse version 0.1.0 from the 0.0.4 working baseline.

Changes made so far for 0.1.0:

```text
Program.cs AppInfo.Version = 0.1.0
DeskPulse.csproj Version = 0.1.0
DeskPulse.csproj AssemblyVersion = 0.1.0.0
DeskPulse.csproj FileVersion = 0.1.0.0
DeskPulse.csproj InformationalVersion = 0.1.0
app.manifest assemblyIdentity version = 0.1.0.0
CHANGELOG.md new 0.1.0 entry added
```

No new functional behaviour has been added yet. Continue feature development from this baseline.


---

## 0.1.0 Quiet Notification Behaviour

Successful normal actions should not show tray balloon notifications. In particular:

- application startup loads straight to tray
- saving settings reloads settings silently

Error dialogs are still retained for startup failures, export failures, cleanup failures, and other actionable errors.
