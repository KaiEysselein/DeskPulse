# DeskPulse

DeskPulse is a small Windows tray app that quietly records selected file activity while you work.

It helps you review what was opened, changed, or saved, and can export clear reports to Excel whenever needed.

## Current version

`0.0.4`

## What it does

- Runs from the Windows system tray
- Monitors selected file types, such as drawings, documents, spreadsheets, PDFs, images, and source files
- Logs file open, write/save, and close activity
- Logs user/session events, including DeskPulse start/stop and PC lock/unlock
- Stores activity locally in a SQLite database
- Exports reports to Excel
- Lets you choose which Excel worksheets and columns are included

## Where data is stored

By default, DeskPulse stores its data here:

```text
%USERPROFILE%\Documents\DeskPulse\
```

Main database:

```text
DeskPulse.db
```

Excel export:

```text
DeskPulse-export.xlsx
```

## Requirements

DeskPulse must be run as Administrator because Windows file activity tracing requires elevated access.

## Build

From the project folder:

```powershell
dotnet clean
dotnet restore
dotnet build
```

## Run from source

```powershell
dotnet run
```

Run from an Administrator terminal.

## Publish portable version

```powershell
dotnet publish .\DeskPulse.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output ".\publish\v0.0.4" `
  /p:PublishSingleFile=false
```

The executable will be created here:

```text
publish\v0.0.4\DeskPulse.exe
```

For now, use the full publish folder rather than copying only the EXE.

## Command-line options

```powershell
DeskPulse.exe -debug
```

Enables diagnostic logging.

```powershell
DeskPulse.exe -maintenance
```

Shows the hidden Maintenance tab in Settings.

```powershell
DeskPulse.exe -uninstall
```

Removes current-user DeskPulse settings and generated log/report files, but keeps the main SQLite database.

## Git hygiene

Do not commit generated build or runtime files, including:

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

## AI-assisted development

DeskPulse was developed with AI-assisted coding support. Final decisions, testing, project direction, and release responsibility remain with the project maintainer.

## License

GNU General Public License v3.0 or later.

SPDX: `GPL-3.0-or-later`
