# DeskPulse 0.2.0.0

DeskPulse is a Windows activity logger split into a privileged Windows service and a normal per-user tray application.

## Architecture

- `DeskPulse.Service` owns ETW file monitoring, application monitoring, Windows startup/session events and database writes.
- `DeskPulse.Tray` provides the tray menu, Settings, View Log, Export, Maintenance and About without requiring elevation.
- `DeskPulse.Shared` contains models, rules, settings, database access and shared monitoring logic.
- The service and tray communicate through the local named pipe `DeskPulse.Service.0.2`.

## Data locations

- Shared settings: `%ProgramData%\DeskPulse\settings.json`
- Activity database and exports: `%USERPROFILE%\Documents\DeskPulse`
- Default database: `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`

The uninstaller removes the program, service, startup registrations and settings, but intentionally preserves the database and exports in `Documents\DeskPulse`.

## Build

Open PowerShell in the repository root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Build.ps1
.\scripts\Publish.ps1
```

Published self-contained x64 applications are created under:

- `publish\service\DeskPulse.Service.exe`
- `publish\tray\DeskPulse.Tray.exe`

The target PC does not require .NET to be installed.

## Installer

After publishing:

```powershell
.\Installer\Build-Installer.ps1
```

The installer is created at:

```text
Installer\Output\DeskPulse_Setup_0.2.0.0.exe
```

The installer registers `DeskPulse.Service` for automatic startup and creates one per-user Startup-folder shortcut for the tray.

## Main controls

- **Pause Logging / Resume Logging** in the tray menu temporarily stops and restarts file and program monitoring without stopping the service.
- **Service status** reports service connectivity and logging state.
- **Settings → Maintenance → Restart Windows Service** restarts the service after UAC approval.
- Only one top-level DeskPulse form is kept open at a time.

## Repository

Project page: https://github.com/KaiEysselein/DeskPulse

License: GNU GPL v3.
