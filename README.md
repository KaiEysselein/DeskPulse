# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected file activity through Windows ETW file I/O tracing and stores the results in a local SQLite database. Excel is used as an export/viewing format only, not as the live log file.

## Version

Current version: `0.0.4`

## Current Features

- Windows system tray application
- File open/write/close activity monitoring
- User/session activity logging for DeskPulse start/stop, PC lock/unlock, and Windows session changes
- ETW-based Windows file I/O tracing
- SQLite live storage using `DeskPulse.db`
- XLSX export for Excel viewing/reporting
- Configurable Excel worksheet selection and worksheet order
- Exported worksheet name: `File Activity`
- Settings window with tabs
- `Files` settings tab
- `Export Options` settings tab
- Hidden `Maintenance` settings tab available only with `-maintenance`
- Registered Windows file type list
- Two-list file type selector for monitored extensions
- Right-hand monitored file type list used as the source of truth
- Temporary-folder file activity exclusion
- Registry-backed option to switch temporary-folder exclusion on/off
- In-app portable cleanup option for DeskPulse registry settings
- In-app buttons to open the DeskPulse data folder and program folder
- Placeholder for future Windows Task Scheduler autostart settings
- Hardcoded exclusion for file activity created by DeskPulse itself
- DeskPulse database/export files excluded from monitoring to avoid recursive logging
- Path export split into:
  - `Full Path`
  - `Folder Path`
  - `File Name`
  - `Extension`
- Network paths reported through `LanmanRedirector` are normalized where possible into mapped-drive style paths
- Startup/error fallback diagnostics written to `%TEMP%\DeskPulse-startup.log`

## Default Data Location

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

## Registry Settings

DeskPulse stores current-user settings here:

```text
HKCU\Software\DeskPulse
```

The app stores values such as:

```text
AppVersion
DataFolderPath
DatabaseFilePath
ExcelExportFilePath
ExtensionsToMonitor
ExportSheets
IgnoreTempFolders
```

Version `0.0.4` adds configurable Excel export worksheets and stores the selected worksheet list/order in the registry. Maintenance tools are hidden during normal use and are available only when DeskPulse is started with `-maintenance`.

Removing registry settings does not delete:

- the DeskPulse program files
- the SQLite database
- the Excel export
- the DeskPulse data folder

## Settings Tabs

### Files

The `Files` tab controls:

- DeskPulse data folder
- temporary-folder exclusion
- monitored file extensions

### Export Options

The `Export Options` tab controls the Excel export layout used by `Open log file in Excel`.

It allows the user to:

- tick which worksheets are created
- sort the worksheet order with Up/Down controls
- open a field sub-tab for each ticked worksheet
- tick which fields/columns appear in each worksheet
- sort the field/column order for each worksheet

Available worksheet options:

- `File Activity`
- `Daily Summary`
- `Summary by Extension`
- `Summary by Process`
- `Errors`
- `User`

The `User` worksheet can include DeskPulse start/stop records, PC lock/unlock records, Windows session logon/logoff records, user name, computer name, process information, app version, and notes.
- `User`

The ticked worksheet list controls which Excel worksheets are created and in what order. Inside each worksheet field tab, the checked fields control which columns are exported and in what order. The default remains `File Activity` only with all standard fields enabled.

### Maintenance

The `Maintenance` tab is hidden during normal use. To show it, start DeskPulse from Command Prompt or PowerShell with:

```powershell
DeskPulse.exe -maintenance
```

Also accepted:

```text
--maintenance
/maintenance
```

The `Maintenance` tab is for portable-app administration and cleanup.

Current functions:

- open the DeskPulse data folder
- open the DeskPulse program folder
- open the diagnostic log
- show active monitored extensions
- remove DeskPulse registry settings for the current Windows user

The tab also contains a disabled placeholder for:

```text
Start DeskPulse with Windows
```

This is reserved for a future version and should use Windows Task Scheduler rather than the normal Startup folder, because DeskPulse requires Administrator privileges for ETW tracing.

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

Run portable cleanup and exit:

```powershell
DeskPulse.exe -uninstall
```

The `-uninstall` mode removes current-user registry settings and generated log/report files, but preserves the SQLite database data.

## Publish Instructions

Recommended current publish method is a portable folder publish.

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

Do not copy only the EXE unless single-file publishing has been separately tested. The safer current deployment is the full publish folder.

## Git Hygiene

Source and documentation files that may be committed:

```text
Program.cs
DeskPulse.csproj
README.md
CHANGELOG.md
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

## AI-Assisted Development Note

DeskPulse was developed with AI-assisted coding support.

AI was used to help draft code, explore implementation options, review errors, improve documentation, and speed up iteration. Final decisions, testing, project direction, and release responsibility remain with the project maintainer.

## License

License intention:

```text
GNU General Public License v3.0 or later
```

SPDX identifier:

```text
GPL-3.0-or-later
```


## Diagnostic Debug Logging

DeskPulse can be started with a debug switch to record accepted monitored ETW file events without flooding the log with normal Windows background activity.

```powershell
DeskPulse.exe -debug
```

For deep troubleshooting only, skipped ETW events can also be logged:

```powershell
DeskPulse.exe -debug -debug-skipped
```

When debug logging is enabled, DeskPulse writes diagnostic entries to:

```text
%USERPROFILE%\Documents\DeskPulse\DeskPulse-diagnostics.log
```

Normal `-debug` mode records accepted monitored events, including raw ETW paths, normalized paths, detected extensions, active monitored extensions, process details, and the accept decision. Use `-debug-skipped` only for short troubleshooting runs because full skip logging can grow very quickly.

Accepted `OPEN` and `WRITE` events are written to the activity database immediately. This avoids missing activity where Windows ETW later emits a `CLOSE` event without a usable filename.

In Settings > Maintenance, the diagnostics section can open the diagnostic log and show the currently active monitored extensions.
