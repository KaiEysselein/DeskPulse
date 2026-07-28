# Version Check — 0.3.4.0

Version **0.3.4.0** is the current milestone release. Historical version numbers in changelog entries and archived verification records are intentionally preserved.

## Active references

- `DeskPulse.Shared` AppInfo: `0.3.4.0`
- Shared, service and tray project versions: `0.3.4.0`
- Inno Setup installer and output filename: `0.3.4.0`
- Publish folders: `dev\publish\v0.3.4.0\service` and `dev\publish\v0.3.4.0\tray`
- Installer: `dev\publish\v0.3.4.0\installer\DeskPulse_Setup_0.3.4.0.exe`
- Current approved installer folder: `releases\current`
- Retained milestone: `releases\v0.3.4.0`
- GitHub tag target: `v0.3.4.0`
- Installer SHA-256: `2A478EA556694E3046C750CCF52256F460C539C0B23900C104A2C998291D3457`

## 0.3.4.0 release checks

- [x] Full Release build completes with zero warnings and zero errors.
- [x] Eight automated tests pass for override precedence, visibility, last-known-good recovery, missing-file fallback, revision warnings, disabled-override restoration, WinForms layout and the opt-in live integrity harness.
- [x] Current User and Administrator Settings render tests pass at compact sizes and simulated 125%, 150% and 200% scaling.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.3.4.0`.
- [x] Inno Setup creates `DeskPulse_Setup_0.3.4.0.exe`.
- [x] The installer is copied to `releases\current` and retained under `releases\v0.3.4.0`.
- [x] The live installer upgraded 0.3.3.2 to 0.3.4.0 with exit code zero.
- [x] Installed service and tray hashes match the published 0.3.4.0 binaries.
- [x] Installed service reports Running and Automatic; the installed tray is running.
- [x] Shipped defaults include explicit rule IDs, revisions, enabled state, UI visibility, action, type, value and reason.
- [x] The administrator override file was preserved across upgrade.
- [x] Config folder and override file ACLs allow only LocalSystem and Administrators.
- [x] No administrator-rule validation errors were produced after installation.
- [x] Elevated read-only SQLite `integrity_check` passes for the installed system and user databases.
- [x] The all-users scheduled tray task was triggered after installation and returned result `0`.
- [x] Machine-wide rules are displayed as locked policy rows and the elevated page shows IDs, source, status and reasons.
- [x] Aggregate candidate diagnostics exclude full paths, user names, SIDs and event contents.

## Existing architecture acceptance

- [x] ProgramData migration, ACL, schema and SQLite integrity passed during 0.3.2.x acceptance.
- [x] SID/session routing and simultaneous-user isolation passed during 0.3.2.x acceptance.
- [x] Named-pipe authorization and system/current-user maintenance boundaries passed.
- [x] Current-user and system log/settings process isolation passed.
- [x] Complete active-tab/date-range export passed with a 209,831-row runtime export.

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database. Rule-candidate diagnostics store bounded aggregate process names and file extensions only.
