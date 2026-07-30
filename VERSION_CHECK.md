# Version Check - 0.4.0.4

Version **0.4.0.4** is the current patch release. Version **0.4.0.0** remains the retained milestone baseline.

## Active references

- `DeskPulse.Shared` AppInfo: `0.4.0.4`
- Shared, service and tray project versions: `0.4.0.4`
- Publish folders: `dev\publish\v0.4.0.4\service` and `dev\publish\v0.4.0.4\tray`
- Installer: `dev\publish\v0.4.0.4\installer\DeskPulse_Setup_0.4.0.4.exe`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.4.exe`
- Retained milestone: `releases\v0.4.0.0`
- GitHub tag target: `v0.4.0.4`
- Installer SHA-256: `48C088B54BE9F49EEB53EB18BC62A8C2964AA1B5BA91EDB4FECD1BB172F11928`

## Final release checks

- [x] Release build completes with zero warnings and zero errors.
- [x] All 75 automated tests pass.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.4.0.4`.
- [x] Inno Setup creates `DeskPulse_Setup_0.4.0.4.exe`.
- [x] The approved installer is copied to `releases\current`.
- [x] Silent upgrade completes with installer exit code 0.
- [x] Installed service and tray report version `0.4.0.4`.
- [x] Installed service is Running with Automatic startup.
- [x] Exactly one tray process is active in the current session.
- [x] Installed service and tray SHA-256 hashes match the published binaries.
- [x] Read-only live SQLite integrity and Calendar schema checks pass.

## Binary hashes

- Published/installed service: `43AF3BB1E1EF061F8CA8C130FF0D42BA78EAB316208B12EE53AADD94C9319576`
- Published/installed tray: `48FE72A2142B76BE452EB80F7A56285C65D38896F31A558DFEC7A59A0FE12E94`

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database.
