# DeskPulse 0.3.2.0 Technical Development Handover

## Purpose and scope

This is the detailed technical handover for the active source under `dev`. It covers architecture, runtime behaviour, safeguards, installation lifecycle logging, build and verification requirements. Repository-level release continuity and GitHub-facing documentation are maintained separately in `..\HANDOVER.md`.

Repository: https://github.com/KaiEysselein/DeskPulse

## Current release baseline

DeskPulse **0.3.2.0** adds a focused administrator-settings process boundary to the accepted 0.3.1.0 baseline.

Included changes since 0.3.0.0:

- Kept ordinary Settings unelevated with General and Rules only.
- Added an **Administrator settings...** tray action that starts a separate process with the Windows UAC `runas` verb.
- Required `--administrator-settings` to validate an elevated administrator token before showing Maintenance only.
- Made the elevated process lifetime match the Administrator settings window lifetime.
- Left service-side named-pipe authorization and the ProgramData system/per-user database architecture explicitly pending.
- Fixed View Log export being cancelled by the generic focus-loss timer when its native Save dialog opened.

- Fixed **Settings → Maintenance → Clean database with current rules...** closing Settings before its confirmation dialog appeared.
- Excluded the Settings form from the generic tray focus-loss auto-close mechanism; Settings now remains open until explicitly closed.
- Kept the database-housekeeping confirmation and completion dialogs explicitly owned by Settings.
- Added User Activity records for DeskPulse installation, update, and same-version reinstallation.
- Corrected Normal, Paused, and Warning tray icon transparency and regenerated their multi-size ICO assets.
- Enabled and migrated User Activity rules for diagnostic-load, service-resource-warning and critical-safety-pause events.
- Staggered diagnostic CPU workers and reserved duty-cycle headroom below the 50% service-side hard cap.
- Completed a static audit of explicitly wired button handlers; no missing named Click handlers were found. This does not replace runtime functional testing.

Historical version references remain unchanged in archived verification and release-history documents.

## Architecture

DeskPulse consists of three .NET 8 Windows projects:

- `DeskPulse.Service`: privileged automatic Windows service; ETW monitoring, application and session monitoring, database writes, named-pipe server, diagnostic load generation and resource safeguards.
- `DeskPulse.Tray`: non-elevated WinForms tray application; Settings, View Log, Export, Maintenance, safeguard status/recovery and named-pipe client.
- `DeskPulse.Shared`: shared settings, models, rules, SQLite access and monitoring logic.

The service owns all SQLite write operations. The tray opens the activity database read-only for views, counts, statistics and exports.

### 0.3.2.x storage migration work in progress

The development working tree now contains the first ProgramData storage foundation:

- live user activity is routed to `C:\ProgramData\DeskPulse\Users\<Windows-SID>\DeskPulse.db`;
- the LocalSystem service resolves the active console user's SID from the session token;
- startup without an interactive session falls back to `C:\ProgramData\DeskPulse\System\DeskPulse-System.db`;
- the legacy Documents database is transferred with SQLite online backup, integrity validation, a retained pre-migration backup and rollback cleanup;
- SID folders receive protected ACLs granting full control only to LocalSystem and administrators, with read-only access for the owning user;
- uninstall preserves the new system and per-user database folders.

The database, attribution, simultaneous-session routing, tray startup and
service-side pipe-authorization foundations described below are now complete.

The migration foundation was runtime-tested on 2026-07-23 against the installed
0.3.2.0 database. SQLite integrity and table counts were checked before and
after migration, protected ACLs were confirmed, service restart retained the
SID database, and uninstall/reinstall preserved it. The test identified and
fixed migration-only SQLite connection pooling and Inno Setup directory
ownership before the final successful run.

The next attribution/routing slice was also completed and runtime-tested on
2026-07-23:

- `ActivityEvents`, `UserEvents` and `ProgramEvents` now store `Scope`,
  `WindowsSid` and `SessionId`;
- existing SID-database rows are backfilled as user-scoped with their database
  SID, while unknown historical session IDs remain null;
- new file and program activity records carry the active user's SID and Windows
  session ID;
- service lifecycle, installation lifecycle, diagnostic and safeguard events
  are routed to the system database with system scope;
- process monitoring now targets the resolved interactive session instead of
  the LocalSystem service's session 0.

The installed upgrade passed schema, routing, restart and SQLite integrity
checks for both the user and system databases. Simultaneous-session routing is
now implemented in the development baseline:

- ETW file records resolve their owning process session and SID before choosing
  a database writer;
- the service keeps a protected writer per resolved SID and retains system
  fallback for unattributable events;
- program monitoring scans every resolvable interactive session rather than
  only the active console session;
- service session-change records use the Windows-supplied session ID;
- database-changing named-pipe commands use the connecting client's process
  session instead of whichever console session happens to be active;
- the tray uses a session-local mutex so each Windows session can have one tray
  instance without blocking trays in other sessions.

Single-session installation, caller-routed pipe operations, duplicate-tray
prevention, service restart and both-database integrity were verified on
2026-07-23. A second standard local account was subsequently tested in parallel:

- the installer-created `DeskPulse Tray` scheduled task launched the tray
  automatically and unelevated at that user's logon;
- one tray ran in each Windows session;
- the second SID database contained only records attributed to that SID and its
  session IDs;
- the legacy Documents database migration is now guarded so only the first SID
  can receive the former single-user history;
- the standard user's Administrator settings action correctly invoked Windows
  UAC for administrator credentials.

The first two-user test exposed and corrected the legacy migration guard before
final acceptance. The contaminated test SID database was removed and recreated
cleanly; the original SID database and backups were unaffected.

### Named-pipe command authorization

The service now resolves the connecting named-pipe client's process ID and
token before accepting state-changing commands:

- mutating user commands are accepted only from the installed
  `DeskPulse.Tray.exe` under protected Program Files;
- database commands continue to target the verified client's SID database;
- diagnostic load control and historical repair additionally require an
  elevated token and local Administrators membership;
- read-only service status remains available to authenticated local clients;
- identity or privilege failures return an explicit pipe error without running
  the command.

Runtime verification on 2026-07-23 confirmed that direct PowerShell clients
could read status but could not delete records, start diagnostic load or invoke
historical repair. The installed unelevated tray remained authorized for its
expected per-user and installation-lifecycle commands.

### System and per-user settings ownership

Settings are now separated by ownership:

- per-user preferences and File, User and App Activity rules are stored in
  `C:\ProgramData\DeskPulse\Users\<Windows-SID>\Settings\settings.json`;
- each Settings folder grants full control only to LocalSystem,
  administrators and its owning SID, while the sibling activity database
  remains read-only to that user;
- machine-wide service safeguard thresholds and restart-pause persistence are
  stored in `C:\ProgramData\DeskPulse\System\settings.json`, writable only by
  LocalSystem and administrators;
- named-pipe settings reloads resolve the verified caller process and refresh
  only that caller SID's cached rules;
- file and program events are evaluated against the rules belonging to the
  process/session SID;
- system-scoped service and lifecycle events bypass per-user rules, so an
  ordinary user cannot suppress system records;
- a fresh SID receives independent defaults and resolves its default data path
  through the SID's registered Windows profile rather than the LocalSystem
  profile.

The installed upgrade was verified on 2026-07-23. The original user's migrated
rule counts matched the legacy settings backup (36 combined, 23 File, 17 User
and 13 App rules), the system settings file contained only the safeguard
properties, the owning user could write its Settings child folder, and its
database remained read-only. A settings backup was retained at
`Documents\DeskPulse Backups\DeskPulse-settings-before-split-2026-07-23.json`.

An administrator UI for configurable system-wide logging rules and a combined
authorized machine-wide database view remain separate backlog work. Current
system lifecycle events follow a non-suppressible service policy.

### 0.3.2.0 storage and security boundary

The administrator-settings split in 0.3.2.0 is a UI and process-lifetime change only. The live database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`, shared settings remain under `%ProgramData%\DeskPulse`, and existing named-pipe command authorization is unchanged. Do not describe this release as having completed service-side administrative security or multi-user data isolation.

The 0.3.2.x continuation is responsible for the service-owned system/per-user database layout, event scope and SID routing, safe migration with rollback, system/per-user rule ownership, access-control changes and service-side verification of administrative pipe clients.

## Service safeguards

DeskPulse includes:

- Once-per-second monitoring of DeskPulse.Service CPU and working-set RAM use.
- Configurable CPU and RAM warning and critical thresholds.
- Configurable sustained warning and critical durations.
- Warning event logging while activity logging continues.
- Critical event logging and immediate safety pause of activity logging.
- Optional persistence of the critical pause across service and Windows restarts.
- **Keep logging paused after restart following a critical trigger** enabled by default.
- Explicit **Resume Logging** recovery.
- Safeguard configuration under **Settings → Maintenance**.
- Validation requiring warning values to remain below their corresponding critical values.

Default safeguard values:

| Level | CPU | Service RAM | Sustained period |
|---|---:|---:|---:|
| Warning | 30% | 30% | 5 seconds |
| Critical | 45% | 45% | 10 seconds |

## Diagnostic safeguard tests

Run these commands from an elevated administrator terminal; the service rejects
diagnostic load control from an unelevated client.

```powershell
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-cpu 40 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-memory 25 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-load --cpu 40 --memory 25 --duration 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --load-status
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --stop-service-load-test
```

Aliases for the combined test are `--load` and `-l`; `--ram` may be used instead of `--memory`.

Safety limits are enforced service-side:

- CPU target never exceeds 50%.
- RAM target never exceeds 50% of total physical memory.
- Requests above 50% are rejected.
- Duration is limited to 1–300 seconds.
- Only one test may run at a time.
- Tests can be stopped from the live test window or command line.

## Installation lifecycle logging

After the service starts successfully, the installer records one User Activity event:

- **DeskPulse installed** when no prior installed executable is detected.
- **DeskPulse updated** when the detected prior version differs from 0.3.2.0.
- **DeskPulse reinstalled** when 0.3.2.0 is installed over the same version.

The installer invokes the tray in non-UI command mode. The tray retries the service named-pipe command for up to 15 seconds, and the service remains the sole SQLite writer. The record contains the installing user, machine, new version, and previous version where applicable. Existing user choices for these event types are not overwritten.

## Tray icon assets

The Normal, Paused, and Warning PNG files use true RGBA transparency. Their ICO files contain transparent frames at 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixels.

## Build, publish and install

Run from the development folder:

```powershell
cd D:\Kai\GitHub\DeskPulse\dev
Get-ChildItem -Path . -Recurse -File | Unblock-File
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Build.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Publish.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Installer.ps1"
Start-Process ".\publish\v0.3.2.0\installer\DeskPulse_Setup_0.3.2.0.exe"
```

The installer build copies the approved installer to:

```text
D:\Kai\GitHub\DeskPulse\releases\current
D:\Kai\GitHub\DeskPulse\releases\v0.3.2.0
```

Release versions whose fourth component is zero are retained under their own release folder. The formal GitHub Release tag is:

```text
v0.3.2.0
```

## Acceptance verification

1. Build succeeds with zero errors.
2. Publish outputs exist under `publish\v0.3.2.0\service` and `publish\v0.3.2.0\tray`.
3. The installer is created as `DeskPulse_Setup_0.3.2.0.exe`.
4. Installer upgrades the accepted 0.3.0.0 or 0.3.0.1 installation.
5. Service and tray report version 0.3.2.0.
6. Exactly one tray instance appears in the active user session.
7. DeskPulse.Service starts automatically and remains responsive.
8. File, App and User Activity records are written normally.
9. **Clean database with current rules...** displays confirmation, performs cleanup, and leaves Settings stable.
10. Install, update, or reinstall writes the correct lifecycle User Activity event.
11. Normal, Paused, and Warning icons display without rectangular or checkerboard backgrounds.
12. A warning-level diagnostic test records one warning and keeps logging active.
13. A critical-level diagnostic test pauses logging and records the critical event.
14. With restart persistence enabled, the critical pause survives service or Windows restart.
15. **Resume Logging** clears the safety pause and restores monitoring.
16. Diagnostic tests cannot exceed 50% CPU or 50% RAM and can be stopped manually.
17. Installer is copied to both `releases\current` and `releases\v0.3.2.0`.
18. GitHub-facing README, CHANGELOG, release notes and handovers identify 0.3.2.0 as current.
19. Ordinary Settings shows General and Rules only without requesting elevation.
20. Administrator settings requests UAC, rejects an unelevated command-line launch, shows Maintenance only, and leaves no elevated DeskPulse process after closing.
21. View Log remains open through the Save dialog and creates the selected Excel workbook.

Build, publish and installer compilation passed on 2026-07-22. Installation, the administrator-settings flow and the corrected export flow were interactively confirmed. Database migration/splitting tests are not applicable to 0.3.2.0 and belong to 0.3.2.x.

## Planned medium feature — Calendar activity view

Add a Calendar view under View Log with month, day and hourly drill-down. The month view will show selectable compact daily summaries; double-clicking a day will show hourly summaries, and double-clicking an hour will open the existing filtered log view. The design must support selectable file, file-type, application, user and Explorer activity metrics and use grouped SQLite queries rather than loading raw rows for calendar summaries.

## Future installer item

Consider optional machine-wide tray startup for all Windows users. Before implementation, resolve concurrent-session behaviour, per-session duplicate prevention, shared versus per-user settings, and database path/ownership. Prefer an **At logon of any user** scheduled task rather than changing only the HKCU Run registration.
