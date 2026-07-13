# DeskPulse Handover — Version 0.1.3.2

## 1. Baseline

This package is the authoritative DeskPulse `0.1.3.2` development baseline.

DeskPulse is a C# / .NET 8 WinForms tray application that stores selected activity in SQLite and exports reports to XLSX. The application requires Administrator privileges for kernel ETW file tracing.

## 2. Active version references

The following active references must all remain `0.1.3.2`:

```text
Program.cs                     AppInfo.Version
DeskPulse.csproj               Version
DeskPulse.csproj               AssemblyVersion
DeskPulse.csproj               FileVersion
DeskPulse.csproj               InformationalVersion
app.manifest                   assemblyIdentity version
README.md                      current version
HANDOVER.md                    current version
VERSION_CHECK.md               expected version
```

Historical versions in `CHANGELOG.md` must not be rewritten.

## 3. Current source layout

```text
Program.cs
AppIcon.cs
DeskPulse.csproj
app.manifest
file-logger.ico
Forms/
  AboutForm.*
  AddLogRuleForm.cs
  ExportDateRangeForm.*
  InstalledAppSelectionForm.*
  LogEntryDetailsForm.*
  RuleCleanupProgressForm.*
  SettingsForm.*
  ViewLogForm.*
README.md
CHANGELOG.md
HANDOVER.md
ROADMAP.md
VERSION_CHECK.md
LICENSE
.gitignore
```

Obsolete standalone Maintenance forms must not be reintroduced. Database housekeeping is a normal Settings tab.

All forms obtain their title-bar icon through `AppIcon.Apply(this)`. Log View sorting is performed in SQLite across the complete filtered result set; selecting a new sort header resets the active tab to page 1.

## 4. Tray behavior

Left-click menu:

```text
View Log...
Settings...
────────────
About
Exit
```

There is no right-click menu and no `-maintenance`, `-m`, `-debug`, or `-debug-skipped` mode.

Portable cleanup through `-uninstall` remains available.

## 5. Settings structure

```text
General
Rules
  File Activity
  App Activity
  User Activity
Export Options
Maintenance
```

### General

Contains application behavior and storage paths.

### Rules

All logging is strict rule-driven:

```text
first matching Include → log
first matching Exclude → suppress
no match → suppress
```

### Maintenance

Contains **Clean database with current rules...**. It saves the displayed rules, evaluates the full database history, deletes records that conflict with current rules, and compacts SQLite with `VACUUM` while showing progress.

## 6. Unified File Activity model

Version `0.1.3.2` removes the separate Folder Activity rules tab and the duplicate Folder Activity log-view tab.

Folder scope is represented by File Activity path wildcards:

```text
C:\Projects\*             files directly in C:\Projects only
C:\Projects\**\*          files directly in C:\Projects and all descendants
C:\Projects\**\*.dwg      recursive DWG files
```

Wildcard semantics:

- `*` does not cross `\`.
- `?` matches one non-separator character.
- `**` crosses folder boundaries.
- `\**\` matches zero or more folder levels, so recursive patterns also match files directly in the selected root folder.
- Matching is case-insensitive.
- A complete `*.*` segment is normalized to `*` for Windows compatibility.

The File Activity editor supports:

- manually entered filename or path patterns
- exact-file selection with **Browse...**
- folder selection with **Add Folder...**
- Include and Exclude rows
- On/off state
- ordered first-match precedence
- Move Up, Move Down, Remove, Duplicate, and Reset Defaults

New entries are appended at the bottom as enabled Include rules.

Explicit App Activity rules continue to take precedence for matching executable files. Equivalent exact executable rules are retained under App Activity rather than duplicated under File Activity.

## 7. Registry schema

Current schema:

```text
SettingsSchemaVersion = 5
```

Active registry layout:

```text
HKCU\Software\DeskPulse
├─ AppVersion
├─ SettingsSchemaVersion
├─ DatabaseFilePath
├─ ExcelExportFilePath
├─ General
│  ├─ DataFolderPath
│  ├─ IgnoreTempFolders
│  ├─ StartWithWindows
│  └─ LogProgramActivity
├─ Rules
│  ├─ FileActivity
│  ├─ AppActivity
│  └─ UserActivity
└─ Export
   └─ Sheets
```

Rule values are JSON arrays of objects containing:

```text
Enabled
RuleType
Action
Value
IncludeSubfolders
```

`IncludeSubfolders` remains in the serialized compatibility model but is always false for active File Activity wildcard rules.

### Schema-5 migration

Legacy Folder Activity rules are read and converted once:

```text
folder rule, Sub off → <folder>\*
folder rule, Sub on  → <folder>\**\*
```

Migration preserves:

- enabled state
- Include/Exclude action
- source rule order

Converted folder rules are placed before broad existing file patterns so their more-specific path decisions retain precedence. Duplicate converted rules are removed. The obsolete active `Rules\FolderActivity` registry value is deleted when settings are saved.

Legacy import packages with schema 1 and a `FolderActivity` list remain supported. Imported folder rules are converted into File Activity patterns. New exports use schema 2 and contain only File Activity and App Activity lists; User Activity remains intentionally excluded from rule-package import/export.

## 8. Monitoring behavior

### File Activity

ETW file open/write/close events are logged only when File Activity rules allow them, except where an explicit App Activity rule takes precedence for an executable.

DeskPulse excludes its own database and XLSX export file. Temporary folders are also excluded when **Ignore temporary folders** is enabled.

### App Activity

Program start/close events are scanned in the current interactive Windows session. App rules are strict first-match rules. A final Include `*` rule may be used to preserve broad monitoring.

DeskPulse’s own application start/stop is logged under App Activity by default.

### User Activity

User/session events use the predefined enabled Include list for lock, unlock, logon, logoff, console, and remote-session events. DeskPulse application start/stop is not recorded in User Activity.

## 9. View Log

View Log contains three tabs:

```text
File Activity
App Activity
User Activity
```

The former Folder Activity view was removed because it duplicated File Activity records rather than representing a separate database stream.

Current behavior:

- selected date range
- newest records first
- 500 records per page
- `<< First Page`
- `< Previous Page`
- `Next Page >`
- `Last Page >>`
- ID column displayed
- one selected row at a time
- **More...** for all stored fields
- **Create Rule** from the selected row
- **Export** for the active tab and current 500-row page only

## 10. Rule creation and cleanup

Creating an Exclude rule from View Log can optionally remove conflicting historical records.

Multiple rows can be selected in View Log and permanently deleted with the **Delete** button. **Create Rule** is available only when exactly one row is selected.

Workflow:

1. Count records affected by the proposed rule.
2. If the count is greater than zero, show the exact number and request confirmation.
3. Add the rule.
4. Delete records in batches with determinate progress.
5. Commit the transaction.
6. Compact SQLite.
7. Reload rules and refresh View Log.

## 11. Rule import/export

Settings includes:

```text
Import Rules...
Export Rules...
```

New exports include File Activity and App Activity rule lists. Old schema-1 exports containing Folder Activity remain importable through conversion.

Imported lists are displayed immediately and written to the registry only when **Save** is pressed.

## 12. Database

Default files:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse.db
%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx
```

Main tables:

```text
ActivityEvents
ProgramEvents
UserEvents
```

Folder Activity is not and has never been a separate table.

## 13. Local verification

Run:

```powershell
dotnet clean
dotnet restore
dotnet build
```

Then test:

```powershell
dotnet run
```

Required functional checks:

- Settings opens with only File, App, and User rule tabs.
- Existing registry Folder Activity rules migrate into File Activity patterns.
- `C:\Test\*` matches files directly in `C:\Test` but not deeper files.
- `C:\Test\**\*` matches direct and nested files.
- File and folder-path Include/Exclude ordering follows first-match behavior.
- App precedence for `.exe` files remains intact.
- View Log contains three tabs and paging works.
- Current-page export works.
- Maintenance cleanup applies the unified rules and compacts the database.
- Rule JSON export/import works, including an older schema-1 package.

## 14. Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ".\publish\v0.1.3.2"
```

Expected executable:

```text
publish\v0.1.3.2\DeskPulse.exe
```

## 15. GitHub handover

Before committing:

- ensure `bin`, `obj`, `.vs`, and publish output are not committed
- run a clean Release build
- test registry migration on a copy or export of existing rules
- update `CHANGELOG.md` only with confirmed behavior
- tag only after local runtime verification


## Cancellable database housekeeping

The Maintenance tab housekeeping progress window has a **Cancel** button. Cancellation is honoured during the full-history scan and between transactional deletion batches. If cancelled before commit, pending deletions are rolled back. SQLite compaction (`VACUUM`) begins only after the deletion transaction commits and cannot be safely interrupted.

## Log View sort routing correction
`ViewLogForm.GetDatabaseSortColumn` identifies File, App, and User grids by their control instances rather than the optional runtime `Name` property. This is required to keep each tab's SQL `ORDER BY` fields matched to its database table.

## Current 0.1.3.2 iteration - Event Type removal
- `Event Type` is no longer shown in any View Log tab.
- `EventType` is no longer stored in `UserEvents` or `ProgramEvents`.
- Existing databases are migrated automatically by dropping the old EventType indexes and columns.
- Event descriptions, user/session logging, application logging, file inferred-action data, rule handling, and all remaining fields are retained.

## User lifecycle logging update
User Activity now records DeskPulse startup/shutdown, Windows startup, Windows logon/logoff, and lock/unlock activity. DeskPulse startup is displayed as `DeskPulse started (possible login)` because application startup may coincide with login but is not itself definitive proof of a new Windows logon. Windows startup is deduplicated per boot using the approximate boot time stored in HKCU.

## Report date-range defaults
- `DatabaseDateRange.GetFirstRecordedDate(...)` finds the earliest `CreatedAt` value across file, app and user activity.
- View Log and the activity export dialog default from that date through today.
- Both report interfaces include a `Today Only` shortcut that changes the start date to today.

## Optional rule creation during log deletion
- Deleting selected records from any View Log tab now displays a dedicated confirmation dialog.
- `Also create exclusion rule(s)` is optional and unchecked by default.
- When enabled, unique exclusion rules are saved only after the selected database records are deleted successfully.

### Current iteration: precise View Log page range
The View Log status line reports the exact range shown on the active page and the total number of matching records, e.g. `Showing 1,001 to 1,500 of 8,999 records.` It updates across all activity tabs after page navigation, sorting, filtering, refreshes and tab changes.
