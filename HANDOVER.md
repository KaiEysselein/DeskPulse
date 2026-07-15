# DeskPulse 0.2.2.1 Handover


Tray-opened forms close automatically after external focus loss, and log views support a persisted 24-hour or 12-hour AM/PM time display.
## 0.2.2.1 File Activity and log-view update

Mapped-drive ETW paths are normalized automatically for newly logged records to their user-facing drive-letter form by removing the redirector token, server, and mapped share root. The temporary user-facing historical repair control has been removed in 0.2.2.1.

- Tray autostart is controlled per user through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`DeskPulse.Tray`); the Windows service starts independently.

## Authoritative baseline

This package is the complete DeskPulse 0.2.2.1 source and handover baseline. Historical entries in `CHANGELOG.md` retain their original version numbers; all active project, installer, application and documentation references are 0.2.2.1.

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

## Completed 0.2.2.1 functionality

### 0.2.2.1 corrective changes

### File Activity log presentation

- File Activity includes a visible **Activity** column based on `InferredAction`, with `ActivityType` as fallback.
- **Activity** is available in the Group by selector.
- File, App and User log tables display times without fractional seconds. Stored database timestamps retain full precision.
- The user-facing **Export Options** tab has been removed; standard export remains available using the retained/default layout.

- Settings uses **Save**, **Save and Close**, and **Close** consistently on editable tabs; Close/X/Esc prompt before discarding unsaved changes.
- Tray menu commands activate on the first click, restore an already-open window, and avoid duplicate top-level forms.
- Tray menu text is **Quit DeskPulse**.
- Database housekeeping streams service-side progress to the tray with real counts and stages.
- Deletion progress is reported only when each 10% threshold is crossed.
- Rules Import provides **Merge** and **Replace** modes.
- Installer startup choice appears only on the final completion page.
- Rule-based database housekeeping processes file and app activity only; user/session history is preserved.
- User Activity uses one Log checkbox per supported predefined event, and unchecked events persist.
- Reset Defaults remains available on File Activity, App Activity, and User Activity.


- Service/tray/shared-library architecture.
- Self-contained win-x64 publish output; target PCs do not require .NET.
- Installer registers and starts the service and starts the tray at user login.
- Comprehensive uninstall cleanup while preserving the Documents database.
- Pause/Resume Logging tray toggle.
- Service status command and corrected named-pipe permissions.
- Maintenance command to restart the Windows service after UAC approval.
- Context-sensitive Settings footer: Save, Save and Close, and Close on editable tabs; Import/Export only on Rules; Close only on Maintenance; the Windows-system tracking toggle saves immediately.
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

DeskPulse 0.2.2.1 normalizes legacy relative data paths to an absolute path under the interactive user's Documents folder. The installer initializes shared settings as the original user before starting the LocalSystem service. The default database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`.



## Windows system activity control

Version 0.2.2.1 includes a global `TrackWindowsSystemActivity` setting, defaulting to `false`. Built-in exclusions are generated in code by `WindowsDefaultExclusions`; they are not persisted as editable user rules. While the option is disabled, DeskPulse excludes the complete Windows installation tree (`%WINDIR%\**`), selected ProgramData locations, the Recycle Bin and high-volume Windows processes. These exclusions are evaluated before user rules and therefore cannot be overridden accidentally by broad Include patterns. The Settings rule grids merge the built-in rules for display and mark them as grey, read-only `Windows default` rows. Service-side database housekeeping uses the same exclusion policy for historical records.

Version 0.2.2.1 retains the configurable `FilteredFileActivityProcesses` list introduced in 0.2.1.7. Matching is case-insensitive; selected processes are excluded from new File Activity logging and are also applied during rule-based historical cleanup. The legacy `LogExplorerFileActivity` setting remains only for migration compatibility and is not exposed as a separate user-facing option.

## Latest correction

- Fixed View Log **Create Rule → clean old data** so database deletion and compaction run through `DeskPulse.Service` instead of the non-elevated tray, preventing SQLite read-only errors.


## Database write ownership

All SQLite write operations initiated by the tray (selected-record deletion, rule cleanup, housekeeping, clearing one table, and clearing all activity) are now executed by DeskPulse.Service through the named pipe. The tray opens the activity database read-only for views, counts, statistics, and exports.

## Baseline lock

Version **0.2.2.1** is locked as the authoritative stabilisation baseline represented by this package. Further feature development should advance to a later version unless a narrowly scoped 0.2.2.1 corrective rebuild is required during compilation or acceptance testing.

## Selected-record deletion stability

- Fixed selected-record deletion waiting indefinitely: service write commands now use the monitor-owned database instance and shared database lock instead of pausing ETW and opening a competing database instance.
- View Log deletion now awaits the service asynchronously so the form remains responsive.

- Rules Import offers **Merge with existing rules** (default) or **Replace existing rules**. Merge updates matching File/App rules and adds new ones without duplicates; User Activity rules are unchanged.

## Mandatory package delivery command chain

Every future DeskPulse source/code ZIP must be accompanied in the same response by the complete PowerShell build, publish, installer-build and installer-launch chain, updated to the package version:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Build.ps1
.\scripts\Publish.ps1
.\Installer\Build-Installer.ps1
Start-Process ".\publish\v0.2.2.1\installer\DeskPulse_Setup_0.2.2.1.exe"
```

