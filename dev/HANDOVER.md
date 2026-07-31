# DeskPulse Technical Development Handover

## Purpose

This is the current technical handover for the active source under `dev`. Repository-level release continuity is maintained in `..\HANDOVER.md`.

Repository: `https://github.com/KaiEysselein/DeskPulse`

## Release and working-tree status

- Released baseline: **0.4.0.5**
- Maintenance release target: **0.4.0.6**
- Current branch: `main`
- Released baseline commit: `d419589` (`Release DeskPulse 0.4.0.5`)

The working tree contains an unreleased Calendar refinement:

- Calendar records are separated into Files, Apps and User Activity tabs.
- Each tab displays and exports only its corresponding activity type.
- Calendar always loads records explicitly selected for Calendar view.
- The former All records/Marked records toggle and registry preference are removed.
- A query test covers selected and unselected records across all three activity sources.

Build and full-suite verification remain pending. Do not describe these changes as released until the complete 0.4.0.6 acceptance workflow has passed.

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

## Maintenance ownership

- User maintenance targets only the verified caller's SID database.
- System maintenance targets only `C:\ProgramData\DeskPulse\System\DeskPulse-System.db`.
- Destructive system maintenance first creates a consistent SQLite backup under `C:\ProgramData\DeskPulse\System\Backups`.
- Confirmation text must name the exact target and confirm that personal SID databases are unaffected.

## Service safeguards

DeskPulse monitors service CPU and working-set RAM once per second.

Default values:

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

## Installation lifecycle logging

After the service starts successfully, the installer records one lifecycle event through the service:

- **DeskPulse installed** when no prior installed executable is detected;
- **DeskPulse updated** when the prior installed version differs from the new version;
- **DeskPulse reinstalled** when the same version is installed again.

The tray retries the named-pipe lifecycle command for up to 15 seconds. The service remains the sole SQLite writer.

## Build and release workflow for 0.4.0.6

Run from the development folder:

```powershell
clear

Set-Location "D:\Kai\GitHub\DeskPulse\dev"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Build.ps1"
dotnet test ".\DeskPulse.sln" -c Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Publish.ps1" -Version "0.4.0.6"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Installer.ps1" -Version "0.4.0.6"
```

Expected generated paths:

```text
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\service
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\tray
D:\Kai\GitHub\DeskPulse\dev\publish\v0.4.0.6\installer\DeskPulse_Setup_0.4.0.6.exe
D:\Kai\GitHub\DeskPulse\releases\current\DeskPulse_Setup_0.4.0.6.exe
```

Because 0.4.0.6 is a patch release, `releases\v0.4.0.0` remains the retained milestone folder and must not be replaced.

The current project and script defaults still identify 0.4.0.5. Version changes to 0.4.0.6 must be applied deliberately as part of the release preparation, then verified before commit.

## 0.4.0.6 acceptance checklist

1. Review the Calendar working-tree diff and confirm the intended UX.
2. Update project and script version defaults to 0.4.0.6.
3. Release build completes with zero errors.
4. Full automated test suite passes.
5. Calendar tests cover selected and unselected File, App and User Activity records.
6. Publish outputs exist under `publish\v0.4.0.6\service` and `publish\v0.4.0.6\tray`.
7. Installer is created as `DeskPulse_Setup_0.4.0.6.exe`.
8. Installer upgrades the accepted 0.4.0.5 installation.
9. Installed service and tray report version 0.4.0.6.
10. Exactly one tray process runs in the active Windows session.
11. Service starts automatically and remains responsive.
12. File, App and User Activity records continue to be written correctly.
13. Calendar shows separate Files, Apps and User Activity tabs.
14. Each Calendar tab shows and exports only its own selected records.
15. Removed Calendar toggle and preference leave no stale UI or registry dependency.
16. Current-user and System logs retain their data-isolation boundaries.
17. Per-user and System maintenance target only their authorized databases.
18. SQLite integrity checks pass for the current user and System databases.
19. Current approved installer is copied to `releases\current`.
20. `releases\v0.4.0.0` remains unchanged.
21. README, CHANGELOG, VERSION_CHECK and both handovers identify 0.4.0.6 only after acceptance.
22. Commit, tag and GitHub release are created only after all required checks pass.

## Generated content and repository housekeeping

The following are regenerable and should not be treated as source history:

- `dev\publish`
- project `bin` folders
- project `obj` folders
- temporary smoke-test projects and outputs

Retain deliberately:

- source and tests;
- release documentation;
- `releases\current`;
- retained milestone folders;
- verification evidence still needed for audit or rollback.

## Planned work after 0.4.0.6

1. Distinct session-only and persistent pause modes.
2. More concurrent-session runtime regression coverage.
3. Further Calendar aggregation and drill-down enhancements where justified.
