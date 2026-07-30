# Version Check - 0.4.0.3

Version **0.4.0.3** is the current patch release. Version **0.4.0.0** remains the retained milestone baseline.

## Active references

- `DeskPulse.Shared` AppInfo: `0.4.0.3`
- Shared, service and tray project versions: `0.4.0.3`
- Publish folders: `dev\publish\v0.4.0.3\service` and `dev\publish\v0.4.0.3\tray`
- Installer: `dev\publish\v0.4.0.3\installer\DeskPulse_Setup_0.4.0.3.exe`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.3.exe`
- Retained milestone: `releases\v0.4.0.0`
- GitHub tag target: `v0.4.0.3`
- Installer SHA-256: `7F1F881F99C0CE5BF4F7419EC256E08BC5C20FF3C15C4DF8EBBF3DDFBE34A815`

## Final release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] All 73 automated tests pass.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.4.0.3`.
- [x] Inno Setup creates `DeskPulse_Setup_0.4.0.3.exe`.
- [x] The approved installer is copied to `releases\current`.
- [x] Silent upgrade completes with installer exit code 0.
- [x] Installed service and tray report version `0.4.0.3`.
- [x] Installed service is Running with Automatic startup.
- [x] Exactly one tray process is active in the current session.
- [x] Installed service and tray SHA-256 hashes match the published binaries.
- [x] Current-user SQLite `integrity_check` and `quick_check` return `ok`.

## Binary hashes

- Published/installed service: `83F009CE8E6D5A4D87C6E85629B09121912D376B9790089F92550D2D6030ADBA`
- Published/installed tray: `EA79E7F56A4BAD5D4B8C67E7C065829D24E1EDA1CE8112B4E286380179B82402`

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database.
