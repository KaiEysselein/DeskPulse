# DeskPulse 0.2.2.2

<p align="center">
  <img src="Resources/DeskPulse_Normal.png" alt="DeskPulse icon" width="220">
</p>


Tray-opened forms close automatically after external focus loss, and log views support a persisted 24-hour or 12-hour AM/PM time display.
Version 0.2.2.2 consolidates application resources under a shared `Resources` folder and provides distinct tray icons for normal, paused, and service-warning states.

- Tray autostart is controlled per user through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`DeskPulse.Tray`); the Windows service starts independently.

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

## Filtered File Activity applications

Settings > General > Activity logging includes **Configure filtered file applications...**. The left list is populated from distinct application/process names already present in File Activity and shows record counts. Move applications to the right to suppress their future File Activity records. Process matching is case-insensitive. **Clean database with current rules...** also removes existing File Activity records attributed to the currently filtered applications.

## Build

Open PowerShell in the repository root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Build.ps1
.\scripts\Publish.ps1
```

Published self-contained x64 applications are created under:

- `publish\v0.2.2.2\service\DeskPulse.Service.exe`
- `publish\v0.2.2.2\tray\DeskPulse.Tray.exe`

The target PC does not require .NET to be installed.

## Installer

After publishing:

```powershell
.\Installer\Build-Installer.ps1
```

The installer is created at:

```text
publish\v0.2.2.2\installer\DeskPulse_Setup_0.2.2.2.exe
```

The installer registers `DeskPulse.Service` for automatic startup. Tray autostart is controlled per user through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` using the value `DeskPulse.Tray`.


## Tray status icons

- **Normal** — DeskPulse is connected and logging is active.
- **Paused** — logging is temporarily paused for the current session.
- **Warning** — the service is unavailable or requires attention.

The authoritative image and icon assets are stored once under `Resources`.

## Main controls

- **Pause Logging / Resume Logging** in the tray menu temporarily stops and restarts file and program monitoring without stopping the service.
- **Service status** reports service connectivity and logging state.
- **Settings → Maintenance → Restart Windows Service** restarts the service after UAC approval.
- Only one top-level DeskPulse form is kept open at a time.

## Repository

Project page: https://github.com/KaiEysselein/DeskPulse

License: GNU GPL v3.


## Absolute data-path migration

DeskPulse 0.2.2.2 normalizes legacy relative data paths to an absolute path under the interactive user's Documents folder. The installer initializes shared settings as the original user before starting the LocalSystem service. The default database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`.



### Windows system activity protection

Settings → General → Activity logging includes **Track Windows system activity**. It is disabled by default, causing DeskPulse to exclude the complete Windows installation folder (`%WINDIR%\**`) together with selected ProgramData locations, the Recycle Bin and high-volume Windows processes. These built-in rules are shown as grey, read-only **Windows default** rows in the File Activity and App Activity rule tabs and take precedence over user Include rules. Database housekeeping applies the same policy to historical records. Enable the option only when Windows internals need to be investigated; it can materially increase event volume, CPU use and database growth.

Settings → General → Activity logging also includes **Log file activity caused by Windows File Explorer**. It is enabled by default. Disable it to suppress File Activity records attributed to `explorer.exe`, including many folder browsing, preview, copy, move, rename, delete, and double-click/open events. Activity subsequently generated by the opened application remains eligible for logging. Database housekeeping applies this setting to historical File Activity records.


## Database write ownership

All SQLite write operations initiated by the tray (selected-record deletion, rule cleanup, housekeeping, clearing one table, and clearing all activity) are now executed by DeskPulse.Service through the named pipe. The tray opens the activity database read-only for views, counts, statistics, and exports.


## Settings button behaviour

- General and Rules use **Save** and **Cancel** because changes are staged.
- Rule import/export controls appear only on the Rules tab.
- Maintenance uses **Close** because its operations execute immediately.
- **Track Windows system activity** is saved immediately when toggled.

- Rules Import offers **Merge with existing rules** (default) or **Replace existing rules**. Merge updates matching File/App rules and adds new ones without duplicates; User Activity rules are unchanged.
