# Version Check — 0.3.3.0

Version **0.3.3.0** is the current release candidate. Historical version numbers in changelog entries and archived verification records are intentionally preserved.

## Active references

- `DeskPulse.Shared` AppInfo: `0.3.3.0`
- Shared, service and tray project versions: `0.3.3.0`
- Inno Setup installer and output filename: `0.3.3.0`
- Publish folders: `dev\publish\v0.3.3.0\service` and `dev\publish\v0.3.3.0\tray`
- Installer: `dev\publish\v0.3.3.0\installer\DeskPulse_Setup_0.3.3.0.exe`
- Permanent milestone folder: `releases\v0.3.3.0`
- GitHub tag target: `v0.3.3.0`
- Published product version commit: `2730f1648ab726ff3bbda7e04c0302c9ba430672`
- Installer SHA-256: `6AB43A09F96F2CA30DDDEBEF0DC8EA8B2BD66F76CCF83DB57F08B79BA4BDF7FB`

## Release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] Self-contained service and tray publish outputs exist under `dev\publish\v0.3.3.0`.
- [x] Inno Setup creates `DeskPulse_Setup_0.3.3.0.exe`.
- [x] Matching installer copies exist under `releases\current` and `releases\v0.3.3.0`.
- [x] Published service and tray report version 0.3.3.0.
- [x] ProgramData migration, ACL, schema and SQLite integrity passed during 0.3.2.x acceptance.
- [x] SID/session routing and simultaneous-user isolation passed during 0.3.2.x acceptance.
- [x] Named-pipe authorization and system/current-user maintenance boundaries passed.
- [x] Current-user and system log/settings process isolation passed.
- [x] Complete active-tab/date-range export passed with a 209,831-row runtime export.
- [ ] Final single-window tray behavior receives a short installed runtime confirmation.

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database.
