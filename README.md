# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms. It records selected file, application, and user/session activity in a local SQLite database and exports reports to XLSX.

## Current version

`0.1.3.1`

This package is the verified source baseline to build, publish, and synchronise with GitHub before further development. The next planned development/release version is `0.1.4.0`.

## Tray menu

DeskPulse has no right-click menu. Left-click the tray icon to open:

```text
View Log...
Settings...
────────────
About
Exit
```

## Settings

Top-level tabs:

```text
General
Rules
Export Options
Maintenance
```

Rules tabs, in order:

```text
Folder Activity
App Activity
File Activity
User Activity
```

Rules are evaluated from top to bottom; the first enabled matching rule wins. Include permits logging, Exclude suppresses logging, and unmatched File/App/User activity is not logged. Folder rules filter File Activity by path and optional subfolder scope.

Rule settings for File, Folder, and App Activity can be exported to or imported from JSON. The Maintenance tab provides **Clean database with current rules...**, which applies the active rules to all stored history, deletes conflicting records, and compacts the SQLite database.

## View Log

View Log contains four views:

```text
Folder Activity
App Activity
File Activity
User Activity
```

The Folder Activity view is derived from File Activity records; there is no separate folder-event table.

Features include:

- database ID shown in each view
- date range filtering
- 500 records per page
- First, Previous, Next, and Last page controls
- current-tab/current-page XLSX export
- single-row selection and full-record **More...** details
- **Create Rule** from a selected record
- optional removal of historical records conflicting with a new Exclude rule
- progress reporting and SQLite compaction during cleanup

## Storage

Default data folder:

```text
%USERPROFILE%\Documents\DeskPulse\
```

Default database:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Default general export:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx
```

Startup fallback errors may be written to:

```text
%TEMP%\DeskPulse-startup.log
```

DeskPulse has no diagnostic/debug logging mode and does not create `DeskPulse-diagnostics.log`.

## Registry

Active settings are stored under:

```text
HKCU\Software\DeskPulse
```

The current settings schema is `4`. JSON rule values are stored below the `Rules` subkey for File Activity, Folder Activity, User Activity, and App Activity.

## Command-line switches

```text
-uninstall
--uninstall
/uninstall
```

The uninstall switch removes current-user settings and generated report/log files while preserving the SQLite database. There is no `-m`, `-maintenance`, `-debug`, or `-debug-skipped` mode.

## Build

From the repository root:

```powershell
dotnet build
```

A fresh verification build:

```powershell
dotnet clean
dotnet restore
dotnet build
```

## Publish a standalone Windows executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output ".\publish\v0.1.3.1"
```

Published executable:

```text
publish\v0.1.3.1\DeskPulse.exe
```

Run and verify the published executable from its final folder before enabling **Start DeskPulse when I log in to Windows**, because the scheduled task stores the exact executable path.

## Repository notes

Generated folders and runtime data are excluded by `.gitignore`, including `bin`, `obj`, `publish`, databases, exports, logs, and packaged executables.

Obsolete files from older iterations should not remain in the repository:

```text
Forms\MaintenanceForm.cs
Forms\MaintenanceProgressForm.cs
Forms\MaintenanceProgressForm.Designer.cs
Forms\MaintenanceProgressForm.resx
```

The project file also excludes those names defensively in case they remain in an older local checkout.
