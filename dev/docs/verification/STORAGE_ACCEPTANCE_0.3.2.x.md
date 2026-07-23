# DeskPulse 0.3.2.x Storage and Security Acceptance

Acceptance was run on Windows on 2026-07-23 against commit
`8d7190e2ffb91469a25c90747146cb1684bae084`.

## Build and packaging

- `scripts\Build.ps1` passed with zero warnings and zero errors.
- `scripts\Publish.ps1` produced self-contained win-x64 service and tray
  executables.
- Inno Setup 6.7.3 compiled the installer successfully.
- The service and tray executables report file version `0.3.2.0` and product
  version containing commit `8d7190e`.
- The publish, `releases\current`, and `releases\v0.3.2.0` installer copies are
  byte-identical.
- Installer SHA-256:
  `5583D2EB2E002EA0203785F86536210A8FFF8FCB58507498A9948B21754E941E`.

Inno Setup emitted its expected warning that an administrator-mode installer
references per-user areas. Compilation completed successfully.

## Installation and runtime

- The newly built installer completed a silent same-version reinstall with
  exit code 0 after Windows UAC approval.
- Installed service and tray hashes exactly match the published executable
  hashes.
- `DeskPulse.Service` restarted in session 0 with Automatic startup and Running
  status.
- The tray started unelevated in session 1 with the installed executable.
- A duplicate `--tray` launch left exactly one tray process in the session.
- The named pipe returned normal service and safeguard status after reinstall.
- Safeguard state was Normal and diagnostic-load state was Idle.
- Direct PowerShell clients could read status but were rejected for
  `RELOAD_SETTINGS` and `START_LOAD_TEST` because they were not the installed
  DeskPulse tray.
- No new service-error or installer-lifecycle-error entries appeared during
  this acceptance run.

The silent installer intentionally did not start the tray because its
post-install action uses `skipifsilent`. The tray was started directly with the
same installed executable, `--tray` argument, and installed working directory
for the remaining runtime checks.

## Scheduled task

The installed `DeskPulse Tray` task has:

- executable `C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe`;
- argument `--tray`;
- working directory `C:\Program Files\DeskPulse\Tray`;
- Limited run level;
- Parallel multiple-instance policy.

The task's stored result remains `1` from the earlier pre-fix run. This
unelevated acceptance process cannot manually start the all-users task, so a
fresh logon remains required to replace that historical result and reconfirm
automatic startup.

## Storage, attribution, and access

- The current user's personal database opened read-only and passed
  `PRAGMA integrity_check` with `ok`.
- `ActivityEvents`, `UserEvents`, and `ProgramEvents` all contain `Scope`,
  `WindowsSid`, and `SessionId`.
- All sampled rows in the current personal database were user-scoped and
  attributed only to the current Windows SID.
- Activity and program counts advanced after service restart, confirming live
  writes continued.
- The current SID root grants read/execute to the owning user and full control
  only to LocalSystem and administrators.
- The current SID Settings folder grants full control to the owning user,
  LocalSystem, and administrators.
- The ordinary user was denied access to the protected System folder and the
  other user's SID folder.

## Manual UI checks still required

- Live check on 2026-07-23 confirmed that both left- and right-clicking the
  updated tray icon open the DeskPulse menu.
- Live check on 2026-07-23 confirmed that current-user Settings opens without UAC,
  shows General, personal rules, and Maintenance without system rules, retains
  the disabled Log folder openings setting, and can close without terminating
  the background tray.
- Live check on 2026-07-23 confirmed that System Settings and Maintenance
  requests UAC, exposes only system-scoped rules and maintenance, closes its
  elevated process, and leaves the unelevated background tray running.
- Live check on 2026-07-23 confirmed that the current-user Log opens without UAC,
  exposes only personal activity, supports paging, sorting, and record details,
  and can close without terminating the background tray.
- Live check on 2026-07-23 confirmed that System Log requests UAC, exposes only
  system/service activity with no personal or destructive controls, supports
  paging, sorting, and record details, and can close without terminating the
  background tray.
- Live check on 2026-07-23 confirmed that Personal and System maintenance
  confirmations identify their respective personal-only and system-only
  targets, never offer another user's database, and can be cancelled without
  deleting data.
- Live check on 2026-07-23 confirmed that log export reads the complete active
  tab and selected date range independently of the display page size. A
  Personal File Activity export contained 209,831 data rows plus its header,
  and the progress-enabled build remained responsive while reporting reading,
  writing, formatting, and saving progress.
- Sign out and back in, then confirm the all-users scheduled task launches one
  unelevated tray and records a successful task result.
