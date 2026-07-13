# DeskPulse Handover — Version 0.1.3.1

## 1. Status

DeskPulse `0.1.3.1` is the current verified source baseline. Use this package for the next local build, standalone publish, and GitHub synchronisation. Do not promote the active version to `0.1.4.0` until the next development cycle is intentionally started.

Version references that must remain aligned:

```text
Program.cs                     AppInfo.Version = 0.1.3.1
DeskPulse.csproj               Version = 0.1.3.1
DeskPulse.csproj               AssemblyVersion = 0.1.3.1
DeskPulse.csproj               FileVersion = 0.1.3.1
DeskPulse.csproj               InformationalVersion = 0.1.3.1
app.manifest                   assemblyIdentity version = 0.1.3.1
README.md                      current version = 0.1.3.1
HANDOVER.md                    current version = 0.1.3.1
VERSION_CHECK.md               expected version = 0.1.3.1
```

Historical version numbers in `CHANGELOG.md` are intentional and must not be mass-replaced.

## 2. Technology and runtime

- C# / .NET 8 WinForms
- target framework: `net8.0-windows`
- SQLite live storage through `Microsoft.Data.Sqlite`
- ETW file I/O monitoring through `Microsoft.Diagnostics.Tracing.TraceEvent`
- XLSX creation through ClosedXML
- administrator elevation required by `app.manifest` for kernel ETW tracing

DeskPulse is a tray application, not a Windows service. Program and user/session monitoring applies to the current interactive session while DeskPulse is running.

## 3. Current source layout

```text
Program.cs
DeskPulse.csproj
app.manifest
file-logger.ico
.gitignore
LICENSE
README.md
CHANGELOG.md
HANDOVER.md
ROADMAP.md
VERSION_CHECK.md
Forms/
  AboutForm.cs
  AboutForm.Designer.cs
  AboutForm.resx
  ExportDateRangeForm.cs
  ExportDateRangeForm.Designer.cs
  ExportDateRangeForm.resx
  InstalledAppSelectionForm.cs
  InstalledAppSelectionForm.Designer.cs
  InstalledAppSelectionForm.resx
  SettingsForm.cs
  SettingsForm.Designer.cs
  SettingsForm.resx
  ViewLogForm.cs
  ViewLogForm.Designer.cs
  ViewLogForm.resx
  LogEntryDetailsForm.cs
  LogEntryDetailsForm.Designer.cs
  LogEntryDetailsForm.resx
  AddLogRuleForm.cs
  RuleCleanupProgressForm.cs
  RuleCleanupProgressForm.Designer.cs
  RuleCleanupProgressForm.resx
  RegisteredFileTypes.cs
```

Obsolete files that must be deleted from an older checkout:

```text
Forms\MaintenanceForm.cs
Forms\MaintenanceProgressForm.cs
Forms\MaintenanceProgressForm.Designer.cs
Forms\MaintenanceProgressForm.resx
```

`DeskPulse.csproj` excludes these names defensively, but they should not be committed.

## 4. Tray behavior

There is no right-click menu.

Left-click menu:

```text
View Log...
Settings...
────────────
About
Exit
```

Normal startup is quiet. Startup/monitoring failures may still display an error dialog.

## 5. Settings

Top-level tabs:

```text
General
Rules
Export Options
Maintenance
```

### General

- start DeskPulse when the user logs in
- log program start and close activity
- data/storage folder configuration
- temporary-folder filtering

Windows startup uses Task Scheduler and stores the exact path of the running executable.

### Rules

Sub-tabs, in order:

```text
Folder Activity
App Activity
File Activity
User Activity
```

Rule behavior:

- enabled rules are evaluated top-to-bottom
- first matching rule wins
- Include logs the matching activity
- Exclude suppresses the matching activity
- unmatched File, App, and User activity is not logged
- Folder Activity rules filter File Activity by folder path
- App Activity rules have explicit precedence for matching executable files
- equivalent exact executable rules are deduplicated between File and App rules

File rules support exact paths, filenames, extensions, and wildcard patterns. App rules support process names, executable names, full executable paths, and the installed-app selector. Folder rules support folder paths and subfolder scope. User rules are predefined event entries with Reset Defaults.

File, Folder, and App rules can be exported/imported as JSON. Imported rules are written to the registry when Settings is saved.

### Export Options

Controls the standard database-to-XLSX worksheets and selected fields.

### Maintenance

Contains one supported housekeeping action:

```text
Clean database with current rules...
```

This saves the currently displayed rules, evaluates all historical records, removes records that would no longer be logged, commits the changes, and runs SQLite `VACUUM`. The operation shows progress and a deletion summary.

This Maintenance tab is a normal Settings tab. There is no Maintenance command-line mode and no separate Maintenance form.

## 6. Rule storage and migration

Registry root:

```text
HKCU\Software\DeskPulse
```

Current settings schema:

```text
SettingsSchemaVersion = 4
```

Active rule lists are stored as JSON under:

```text
HKCU\Software\DeskPulse\Rules\FileActivity
HKCU\Software\DeskPulse\Rules\FolderActivity
HKCU\Software\DeskPulse\Rules\UserActivity
HKCU\Software\DeskPulse\Rules\AppActivity
```

Rule records preserve:

```text
Enabled
RuleType
Action
Value
IncludeSubfolders
```

Legacy root values are read for migration compatibility. Once schema 4 is active, intentional empty lists are preserved rather than silently repopulated.

## 7. Logging behavior

### File Activity

ETW open/write/close events are considered only when an enabled File Include rule matches, unless an explicit App rule determines the result for a matching executable. Folder rules then apply path-based filtering.

### App Activity

Program start/stop events are logged only when an enabled App Include rule matches. Default rules exclude known noisy background processes and include DeskPulse itself plus a final catch-all Include rule unless the user removes it.

DeskPulse start/stop appears in App Activity when program activity logging is enabled and the active App rules permit it.

### User Activity

User/session events are logged only when enabled User Include rules match. Default events include DeskPulse lifecycle, lock/unlock, logon/logoff, console connect/disconnect, and remote connect/disconnect where available.

### Folder Activity

Folder Activity is not a separate database event stream. It is a folder-oriented view/filter of File Activity records.

## 8. Database

Default database:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
```

Primary tables:

```text
ActivityEvents
ProgramEvents
UserEvents
```

SQLite uses WAL mode. Cleanup operations may delete records in batches and compact the database with `VACUUM`.

## 9. View Log

Tabs:

```text
Folder Activity
App Activity
File Activity
User Activity
```

Current behavior:

- inclusive From/To date range
- newest records first
- database ID displayed as the first column
- 500 rows per page
- independent page position per tab
- `<< First Page`, `< Previous Page`, `Next Page >`, `Last Page >>`
- Export button to the right of pager controls
- export includes only the active tab and current 500-row page
- single-row selection
- `More...` opens all stored fields for the record
- `Create Rule` is enabled when one row is selected

Create Rule can optionally remove historical records conflicting with a new Exclude rule. DeskPulse counts affected records, requests confirmation when deletion is required, shows progress, deletes matching history, compacts the database, reloads settings, and refreshes the viewer.

## 10. Debug and diagnostics

DeskPulse has no debug logging feature.

Removed/unsupported switches:

```text
-debug
-debug-skipped
-maintenance
-m
```

DeskPulse does not create `DeskPulse-diagnostics.log`.

A last-resort startup failure log may still be written to:

```text
%TEMP%\DeskPulse-startup.log
```

## 11. Supported command-line mode

Portable cleanup/uninstall:

```powershell
DeskPulse.exe -uninstall
```

Accepted prefixes are `-`, `--`, and `/`. This removes current-user registry settings and generated files while preserving the SQLite database.

## 12. Local build verification

Normal iteration:

```powershell
dotnet build
```

Fresh verification:

```powershell
dotnet clean
dotnet restore
dotnet build
```

Expected result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Runtime smoke test:

```powershell
dotnet run
```

Do not use `-m`; that switch no longer exists.

Verify:

- tray icon appears
- left-click menu matches the current menu
- no right-click menu appears
- Settings opens all four top-level tabs
- Rules opens Folder, App, File, and User tabs in that order
- View Log loads and pages through 500-row pages
- current-page export creates and opens XLSX
- Create Rule can add a rule and optionally clean conflicting history
- Maintenance housekeeping completes with progress
- About reports `0.1.3.1`
- Exit records DeskPulse shutdown where rules permit

## 13. Publish a standalone executable

From the repository root:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output ".\publish\v0.1.3.1"
```

Expected executable:

```text
publish\v0.1.3.1\DeskPulse.exe
```

Test the published executable from the final deployment path. If Start with Windows is enabled, disable/re-enable it from the final folder so Task Scheduler stores the correct path.

## 14. GitHub synchronisation checklist

Before commit:

```powershell
dotnet clean
dotnet restore
dotnet build
```

Confirm that Git does not include:

```text
bin/
obj/
publish/
.vs/
*.db
*.db-wal
*.db-shm
*.xlsx
*.log
*.exe
*.zip
```

Recommended commit message:

```text
Release DeskPulse 0.1.3.1 verified baseline
```

Recommended tag after final smoke testing:

```text
v0.1.3.1
```

Suggested release title:

```text
DeskPulse 0.1.3.1
```

## 15. Next development cycle

After this baseline is committed, tagged, and published, the next intentional development iteration should change all active version references to:

```text
0.1.4.0
```

Do not alter historical changelog headings when changing the active version.
