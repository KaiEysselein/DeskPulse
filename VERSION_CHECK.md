# Version Check - 0.4.0.5

Version **0.4.0.5** is the current patch release. Version **0.4.0.0** remains the retained milestone baseline.

## Active references

- `DeskPulse.Shared` AppInfo: `0.4.0.5`
- Shared, service and tray project versions: `0.4.0.5`
- Publish folders: `dev\publish\v0.4.0.5\service` and `dev\publish\v0.4.0.5\tray`
- Installer: `dev\publish\v0.4.0.5\installer\DeskPulse_Setup_0.4.0.5.exe`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.5.exe`
- Retained milestone: `releases\v0.4.0.0`
- GitHub tag target: `v0.4.0.5`
- Installer SHA-256: `AA3BC8DA12B06E49EAEACD3B0A9FC87860169C2D458ECAB109BAE42AD06A3337`

## Final release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] All 79 automated tests pass.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.4.0.5`.
- [x] Inno Setup creates `DeskPulse_Setup_0.4.0.5.exe`.
- [x] The approved installer is copied to `releases\current`.
- [x] Silent upgrade completes with installer exit code 0.
- [x] Installed service and tray report version `0.4.0.5`.
- [x] Installed service is Running with Automatic startup.
- [x] Exactly one tray process is active in the current session.
- [x] Installed service and tray SHA-256 hashes match the published binaries.
- [x] Read-only live SQLite integrity and Calendar schema checks pass.

## Binary hashes

- Published/installed service: `13FE348AAF695AE4D58514FF8FB6CCA8EA26DFF0D1C8EEF616933801151A55D5`
- Published/installed tray: `180123C4DAF85AB2AC7D2CE82C88ECD8C17EA34A8D7CFCE7983E4541BC1ABD02`

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database.
