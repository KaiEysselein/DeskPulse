# DeskPulse Technical Development Handover

## Purpose

This is the current technical handover for the active source under `dev`. Repository-level release continuity is maintained in `..\HANDOVER.md`.

Repository: `https://github.com/KaiEysselein/DeskPulse`

## Release status

- Current release: **0.4.0.6**
- Current branch: `main`
- Release code commit: `6fd84ca`
- Release tag target: `v0.4.0.6`
- Automated tests: **81 passed, 0 failed, 0 skipped**
- Current installer: `..\releases\current\DeskPulse_Setup_0.4.0.6.exe`

### 0.4.0.6 release content

- Calendar records are separated into Files, Apps and User Activity tabs.
- Each tab displays and exports only its corresponding activity type.
- Calendar loads only records explicitly selected for Calendar view.
- The former All records/Marked records toggle and registry preference are removed.
- Grouping and expanded/collapsed group state are independent for each Calendar tab.
- The installer no longer displays an Additional Tasks page.
- Fresh installs enable the desktop shortcut and startup message by default.
- Upgrades and same-version reinstalls preserve existing shortcut and startup-message choices.
- Repository handovers and retained generated output were cleaned up.

## Solution architecture

DeskPulse consists of three .NET 8 Windows projects:

- `DeskPulse.Service`: privileged automatic Windows service responsible for ETW monitoring, application and session monitoring, database writes, named-pipe service commands, diagnostic load generation and resource safeguards.
- `DeskPulse.Tray`: non-elevated WinForms tray application providing current-user and administrator entry points, logs, settings, export, maintenance and safeguard recovery.
- `DeskPulse.Shared`: shared settings, models, rules, SQLite access and monitoring logic.

The service owns all SQLite write operations. Tray and log processes open activity databases read-only for views, counts, statistics and exports.

## Storage, identity and session routing

- User activity is stored under `C:\ProgramData\DeskPulse\Users\<Windows-SID>\DeskPulse.db`.
- System activity is stored under `C:\ProgramData\DeskPulse\System\DeskPulse-System.db`.
- File and application events are attributed by process session and SID.
- Program monitoring covers resolvable interactive Windows sessions.
- Unattributable or machine-scoped events fall back to the protected System database.
- The tray uses a session-local mutex, allowing one tray instance per Windows session.
- The legacy Documents database migration is guarded so only the first eligible SID receives the former single-user history.

## Settings ownership

- Per-user preferences and File, App and User Activity rules are stored under `C:\ProgramData\DeskPulse\Users\<Windows-SID>\Settings\settings.json`.
- Machine-wide safeguard settings and System Event rules are stored in `C:\ProgramData\DeskPulse\System\settings.json`.
- User settings and rule reloads are resolved from the verified caller SID.
- System-scoped events are evaluated only against the protected system rule set.

## Security boundary and named-pipe authorization

- Mutating user commands are accepted only from the installed `DeskPulse.Tray.exe` under protected Program Files.
- User database commands target the verified client's SID database.
- Diagnostic load control and historical repair additionally require elevation and local Administrators membership.
- Read-only service status is available to authenticated local clients.
- Identity, installation-path or privilege failures return explicit errors and do not execute the command.

DeskPulse intentionally provides no combined all-users log and no administrator path into another user's personal database.

## User and administrator interfaces

- **Current User → Log...** opens only the calling user's SID database.
- **Current User → Settings...** exposes per-user General, Rules and user-scoped Maintenance functions.
- Current-user Log and Settings run as separate unelevated processes so closing them cannot terminate the background tray.
- **Administrator → System Log...** opens only the protected System database through UAC.
- **Administrator → System Settings and Maintenance...** exposes machine-wide settings, System Event rules and System database maintenance.
- Elevated processes terminate when their windows close.
- Only one DeskPulse form is opened from a tray instance at a time.

## Calendar model

- Calendar loads only records with `ShowInCalendarView <> 0`.
- One multi-source query loads File, App and User Activity.
- Files, Apps and User Activity are presented in separate tabs.
- Each tab filters and exports only its corresponding activity type.
- Grouping mode and expanded-group keys are stored per activity tab.
- Switching tabs must not clear, inherit or corrupt another tab's grouping state.
- Header double-click affects only the active tab.
- Group-row double-click changes only the active tab's expanded-group set.

## Installer behaviour

- The Additional Tasks page is not shown.
- Fresh installs create the desktop shortcut and enable the startup message.
- Upgrade and same-version reinstall detection uses the existing installed DeskPulse executable version.
- Upgrade/reinstall preserves the desktop shortcut by checking whether it existed before files are replaced.
- Upgrade/reinstall leaves the saved startup-message preference untouched.
- Installation lifecycle records distinguish Installed, Updated and Reinstalled.

## Maintenance ownership

- User maintenance targets only the verified caller's SID database.
- System maintenance targets only `C:\ProgramData\DeskPulse\System\DeskPulse-System.db`.
- Destructive system maintenance first creates a consistent SQLite backup under `C:\ProgramData\DeskPulse\System\Backups`.
- Confirmation text must name the exact target and confirm that personal SID databases are unaffected.

## Service safeguards

DeskPulse monitors service CPU and working-set RAM once per second.

| Level | CPU | Service RAM | Sustained period |
|---|---:|---:|---:|
| Warning | 30% | 30% | 5 seconds |
| Critical | 45% | 45% | 10 seconds |

Behaviour:

- warning events are logged while activity logging continues;
- critical events are logged and activity logging is safety-paused;
- critical pause persistence across restarts is configurable and enabled by default;
- Resume Logging clears the safety pause;
- validation requires warning values below their corresponding critical values.

Diagnostic test commands must be run from an elevated terminal:

```powershell
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-cpu 40 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-memory 25 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-load --cpu 40 --memory 25 --duration 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --load-status
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --stop-service-load-test
```

Service-side limits:

- CPU target maximum: 50%.
- RAM target maximum: 50% of total physical memory.
- Duration: 1–300 seconds.
- Only one load test may run at a time.

## Build and release workflow

Run from the development folder:

```powershell
clear

Set-Location "D:\Kai\GitHub\DeskPulse\dev"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Build.ps1"
dotnet test ".\tests\DeskPulse.Tests\DeskPulse.Tests.csproj" --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Publish.ps1" -Version "0.4.0.6"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Installer.ps1" -Version "0.4.0.6"
```

Generated paths:

```text
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\service
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\tray
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\installer\DeskPulse_Setup_0.4.0.6.exe
D:\Kai\GitHub\DeskPulse\releases\current\DeskPulse_Setup_0.4.0.6.exe
```

`releases\v0.4.0.0` remains the retained milestone folder.

## 0.4.0.6 verification summary

- Release build succeeded.
- 81 automated tests passed.
- Publish succeeded.
- Inno Setup created the installer.
- Upgrade installation and runtime startup succeeded.
- Service reported Running with Automatic startup.
- One service and one tray process were active.
- Current-user Log and record deletion worked.
- Calendar tab separation and independent state were accepted manually.
- Installer task-page removal and upgrade preference preservation were accepted manually.
- Installer SHA-256 is recorded in `..\VERSION_CHECK.md`.

## Generated content and repository housekeeping

Regenerable content:

- `dev\publish`
- project `bin` folders
- project `obj` folders
- temporary smoke-test projects and outputs

Retain deliberately:

- source and tests;
- release documentation;
- `releases\current`;
- `releases\v0.4.0.0`;
- verification evidence still required for audit or rollback.

## Planned work after 0.4.0.6

1. Distinct session-only and persistent pause modes.
2. More concurrent-session runtime regression coverage.
3. Further Calendar aggregation and drill-down enhancements where justified.
