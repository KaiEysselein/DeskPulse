# Version Check — 0.3.2.0

Version **0.3.2.0** is the current release baseline. Historical version numbers in older changelog entries and archived verification records are intentionally preserved.

## Verified active references

- `DeskPulse.Shared` AppInfo: `0.3.2.0`
- `DeskPulse.Shared.csproj`: `0.3.2.0`
- `DeskPulse.Service.csproj`: `0.3.2.0`
- `DeskPulse.Tray.csproj`: `0.3.2.0`
- Inno Setup installer and output filename: `0.3.2.0`
- Publish folders: `dev\publish\v0.3.2.0\service` and `dev\publish\v0.3.2.0\tray`
- Installer: `dev\publish\v0.3.2.0\installer\DeskPulse_Setup_0.3.2.0.exe`
- Permanent milestone folder: `releases\v0.3.2.0`
- GitHub tag target: `v0.3.2.0`
- Active README, handovers, roadmap, release notes and changelog: `0.3.2.0`

## Verified release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] Self-contained service and tray publish outputs exist under `dev\publish\v0.3.2.0`.
- [x] Inno Setup creates `DeskPulse_Setup_0.3.2.0.exe`.
- [x] Matching installer copies exist under `releases\current` and `releases\v0.3.2.0`.
- [x] Installed service and tray report 0.3.2.0.
- [x] `DeskPulse.Service` runs automatically and one ordinary tray process runs in the interactive session.
- [x] Ordinary Settings shows General and Rules only without requesting elevation.
- [x] Administrator settings uses UAC, validates elevation, shows Maintenance only and ends with its window.
- [x] View Log remains open through the Save dialog and creates an Excel workbook.
- [x] The existing database, WAL/SHM files, backups and rule exports remain present after upgrade.

## 0.3.2.0 scope boundary

The activity database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`. Version 0.3.2.0 did not move or split the database and did not complete service-side administrative authorization.

The 0.3.2.x continuation covers ProgramData system/per-user databases, Windows-SID routing, safe migration and rollback, rule ownership separation, access controls and named-pipe client authorization.
