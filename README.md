# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms. It records selected file, application, and user/session activity in a local SQLite database and exports data to Excel.

## Version

Current development version: `0.1.3.2`

## Current tray menu

Left-click the tray icon:

```text
View Log...
Settings...
────────────
About
Exit
```

There is no right-click tray menu.

## Rules model

DeskPulse uses strict ordered rules. Rules are evaluated from top to bottom and the first matching rule wins.

```text
Matching Include → log
Matching Exclude → do not log
No matching rule → do not log
```

Settings contains three rule tabs:

```text
File Activity
App Activity
User Activity
```

The previous separate Folder Activity rule list has been removed. Folder filtering is now expressed as File Activity path wildcards.

### File Activity patterns

Examples:

```text
*.xlsx                         all XLSX filenames
Report*.pdf                    matching PDF filenames
C:\Projects\Specific.dwg       one exact file
C:\Projects\*                  all files directly in C:\Projects
C:\Projects\**\*               all files in C:\Projects and every subfolder
C:\Projects\**\*.dwg           all DWG files recursively
```

Wildcard meaning:

- `*` matches characters within one path segment and never crosses a folder separator.
- `?` matches one character within one path segment.
- `**` matches zero or more complete folder levels.
- Matching is case-insensitive on Windows.
- A complete `*.*` path segment is treated as `*` for Windows-style compatibility.

The File Activity tab includes **Browse...** for an exact file and **Add Folder...** for creating either a one-folder pattern (`\*`) or a recursive pattern (`\**\*`).

Explicit App Activity rules retain precedence for matching executable files.

## Registry settings

Current settings schema: `5`

```text
HKCU\Software\DeskPulse
├─ SettingsSchemaVersion
├─ General
├─ Rules
│  ├─ FileActivity
│  ├─ AppActivity
│  └─ UserActivity
└─ Export
```

Rule lists are stored as JSON. On first load of schema 5, legacy `Rules\FolderActivity` rules are converted into File Activity wildcard rules:

```text
folder only       → <folder>\*
include subfolders → <folder>\**\*
```

After migration, the obsolete `Rules\FolderActivity` value is removed.

## View Log

View Log contains:

```text
File Activity
App Activity
User Activity
```

Each tab loads 500 records per page and supports First, Previous, Next, and Last navigation. Export writes only the active tab and currently displayed page. Selecting one row enables **Create Rule** and **More...** opens the complete stored record.

## Database housekeeping

Settings → Maintenance contains **Clean database with current rules...**. It reapplies the currently displayed rules to all historical records, removes records that would no longer be logged, and compacts the SQLite database.

## Build

```powershell
dotnet build
```

## Publish a standalone executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ".\publish\v0.1.3.2"
```

Output:

```text
publish\v0.1.3.2\DeskPulse.exe
```

DeskPulse requires Administrator privileges because kernel ETW file monitoring requires elevation.


### Cancelling database housekeeping

The Maintenance cleanup progress window includes **Cancel**. Cancelling during the scan or deletion phase stops the operation and rolls back uncommitted deletions. Database compaction starts only after deletions are committed and must then finish.
