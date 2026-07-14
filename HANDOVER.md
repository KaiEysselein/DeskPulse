# DeskPulse 0.2.0.1 Handover

- Tray autostart is controlled per user through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`DeskPulse.Tray`); the Windows service starts independently.

## Authoritative baseline

This package is the complete DeskPulse 0.2.0.1 source and handover baseline. Historical entries in `CHANGELOG.md` retain their original version numbers; all active project, installer, application and documentation references are 0.2.0.1.

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
- Tray startup: per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value named `DeskPulse.Tray`
- Service/tray pipe ACL permits an authenticated non-elevated user to query status and send supported commands.

User Activity includes Windows startup, logon/logoff, lock/unlock, `DeskPulse service started` and `DeskPulse started (possible login)`.

## Data and settings

- Shared settings: `%ProgramData%\DeskPulse\settings.json`
- Default data folder: `%USERPROFILE%\Documents\DeskPulse`
- Default database: `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`
- Default export: `%USERPROFILE%\Documents\DeskPulse\DeskPulse-export.xlsx`

Legacy registry settings are migrated into the shared settings file. The uninstaller intentionally preserves `Documents\DeskPulse`, including the activity database and exports, while removing program files, service registration, shared settings and startup registrations.

## Completed 0.2.0.1 functionality

- Service/tray/shared-library architecture.
- Self-contained win-x64 publish output; target PCs do not require .NET.
- Installer registers and starts the service and starts the tray at user login.
- Comprehensive uninstall cleanup while preserving the Documents database.
- Pause/Resume Logging tray toggle.
- Service status command and corrected named-pipe permissions.
- Maintenance command to restart the Windows service after UAC approval.
- Context-sensitive Settings footer: Save/Cancel on editable tabs, Import/Export only on Rules, and Close only on Maintenance; the Windows-system tracking toggle saves immediately.
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


## Absolute data-path migration

DeskPulse 0.2.0.1 normalizes legacy relative data paths to an absolute path under the interactive user's Documents folder. The installer initializes shared settings as the original user before starting the LocalSystem service. The default database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`.



## Windows system activity control

Version 0.2.0.1 includes a global `TrackWindowsSystemActivity` setting, defaulting to `false`. Built-in exclusions are generated in code by `WindowsDefaultExclusions`; they are not persisted as editable user rules. While the option is disabled, DeskPulse excludes the complete Windows installation tree (`%WINDIR%\**`), selected ProgramData locations, the Recycle Bin and high-volume Windows processes. These exclusions are evaluated before user rules and therefore cannot be overridden accidentally by broad Include patterns. The Settings rule grids merge the built-in rules for display and mark them as grey, read-only `Windows default` rows. Service-side database housekeeping uses the same exclusion policy for historical records.

## Latest correction

- Fixed View Log **Create Rule → clean old data** so database deletion and compaction run through `DeskPulse.Service` instead of the non-elevated tray, preventing SQLite read-only errors.


## Database write ownership

All SQLite write operations initiated by the tray (selected-record deletion, rule cleanup, housekeeping, clearing one table, and clearing all activity) are now executed by DeskPulse.Service through the named pipe. The tray opens the activity database read-only for views, counts, statistics, and exports.

## Baseline lock

Version **0.2.0.1** is locked as the authoritative stabilisation baseline represented by this package. Further feature development should advance to a later version unless a narrowly scoped 0.2.0.1 corrective rebuild is required during compilation or acceptance testing.

## Selected-record deletion stability

- Fixed selected-record deletion waiting indefinitely: service write commands now use the monitor-owned database instance and shared database lock instead of pausing ETW and opening a competing database instance.
- View Log deletion now awaits the service asynchronously so the form remains responsive.
