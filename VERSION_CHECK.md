# Version Check — 0.3.3.1

Version **0.3.3.1** is the current patch release. Historical version numbers in changelog entries and archived verification records are intentionally preserved.

## Active references

- `DeskPulse.Shared` AppInfo: `0.3.3.1`
- Shared, service and tray project versions: `0.3.3.1`
- Inno Setup installer and output filename: `0.3.3.1`
- Publish folders: `dev\publish\v0.3.3.1\service` and `dev\publish\v0.3.3.1\tray`
- Installer: `dev\publish\v0.3.3.1\installer\DeskPulse_Setup_0.3.3.1.exe`
- Current approved installer folder: `releases\current`
- Retained milestone baseline: `releases\v0.3.3.0`
- GitHub tag target: `v0.3.3.1`
- Installer SHA-256: `F505908EBE403DDB7835ED838231255D06FF1C154E18A928AC142D3365DA480F`

## Release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] Self-contained service and tray publish outputs exist under `dev\publish\v0.3.3.1`.
- [x] Inno Setup creates `DeskPulse_Setup_0.3.3.1.exe`.
- [x] The installer is copied to `releases\current`; the 0.3.3.0 milestone remains retained.
- [x] Published and installed service and tray report version 0.3.3.1.
- [x] ProgramData migration, ACL, schema and SQLite integrity passed during 0.3.2.x acceptance.
- [x] SID/session routing and simultaneous-user isolation passed during 0.3.2.x acceptance.
- [x] Named-pipe authorization and system/current-user maintenance boundaries passed.
- [x] Current-user and system log/settings process isolation passed.
- [x] Complete active-tab/date-range export passed with a 209,831-row runtime export.
- [x] Standalone Log and Settings windows are fitted to the active screen and explicitly activated.
- [x] The installed Current User Log created a visible `DeskPulse - Log (Current User)` main window.

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database.
