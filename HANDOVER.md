# DeskPulse 0.2.0.0 Handover

## Authoritative baseline

This package is the complete DeskPulse 0.2.0.0 source and handover baseline. Historical entries in `CHANGELOG.md` retain their original version numbers; all active project, installer, application and documentation references are 0.2.0.0.

Repository: https://github.com/KaiEysselein/DeskPulse

## Architecture

DeskPulse is split into three .NET 8 Windows projects:

- `DeskPulse.Service`: privileged Windows service; ETW file monitoring, application monitoring, Windows startup and session events, database writes and named-pipe server.
- `DeskPulse.Tray`: non-elevated WinForms tray application; Settings, View Log, Export, Maintenance, About and named-pipe client.
- `DeskPulse.Shared`: shared models, settings, rules, SQLite access and monitoring logic.

The service remains active when the tray closes or no interactive user is logged in. The tray does not request administrator elevation during normal operation.

## Service and tray behaviour

- Windows service name: `DeskPulse.Service`
- Named pipe: `DeskPulse.Service.0.2`
- Service startup mode: Automatic
- Tray startup: one shortcut in the current user's Startup folder
- Service/tray pipe ACL permits an authenticated non-elevated user to query status and send supported commands.

User Activity includes Windows startup, logon/logoff, lock/unlock, `DeskPulse service started` and `DeskPulse started (possible login)`.

## Data and settings

- Shared settings: `%ProgramData%\DeskPulse\settings.json`
- Default data folder: `%USERPROFILE%\Documents\DeskPulse`
- Default database: `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`
- Default export: `%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx`

Legacy registry settings are migrated into the shared settings file. The uninstaller intentionally preserves `Documents\DeskPulse`, including the activity database and exports, while removing program files, service registration, shared settings and startup registrations.

## Completed 0.2.0.0 functionality

- Service/tray/shared-library architecture.
- Self-contained win-x64 publish output; target PCs do not require .NET.
- Installer registers and starts the service and starts the tray at user login.
- Comprehensive uninstall cleanup while preserving the Documents database.
- Pause/Resume Logging tray toggle.
- Service status command and corrected named-pipe permissions.
- Maintenance command to restart the Windows service after UAC approval.
- Single active top-level DeskPulse form.
- Consistent form icons.
- Full-result Log View sorting with reset to page 1.
- Precise paging status such as `Showing 1,001 to 1,500 of 8,999 records.`
- Default report range from earliest database record through today, plus **Today Only**.
- Optional exclusion-rule creation when deleting records.
- Startup, login, logout, lock and unlock logging under User Activity.
- Removed the obsolete Event Type field from active database/log/UI use.

## Build and release verification

Run from the repository root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Build.ps1
.\scripts\Publish.ps1
.\Installer\Build-Installer.ps1
```

Before release, verify:

1. Build succeeds with zero errors.
2. Installer upgrades an existing installation.
3. Exactly one tray icon appears after restart.
4. `DeskPulse.Service` is Running after restart.
5. Service status, pause/resume and service restart work from the tray.
6. File, App and User Activity records are written.
7. View Log sorting, pagination, deletion and export work.
8. Uninstall removes service, application, startup entries and settings.
9. `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db` remains after uninstall.

## Important deployment rule

Use either the installer or the manual service scripts for a test installation, not both at the same time. Before changing installation methods, remove the previous service/startup registration to avoid duplicate tray instances.
