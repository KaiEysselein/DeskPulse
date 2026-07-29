# Version Check — 0.3.4.4

Version **0.3.4.4** is the current maintenance release. Historical version numbers in changelog entries and archived verification records are intentionally preserved.

## Active references

- `DeskPulse.Shared` AppInfo: `0.3.4.4`
- Shared, service and tray project versions: `0.3.4.4`
- Inno Setup installer and output filename: `0.3.4.4`
- Publish folders: `dev\publish\v0.3.4.4\service` and `dev\publish\v0.3.4.4\tray`
- Installer: `dev\publish\v0.3.4.4\installer\DeskPulse_Setup_0.3.4.4.exe`
- Current approved installer folder: `releases\current`
- Retained milestone: `releases\v0.3.4.0` (quarantined by endpoint protection)
- GitHub tag target: `v0.3.4.4`
- Installer SHA-256: `36BDBEBF121E7A55D27F75FAF5E716EAE90BCE69FAFFE54F5E49E425A5BE04E9`

## 0.3.4.4 release checks

- [x] Full Release build completes with zero warnings and zero errors.
- [x] All 24 automated tests pass.
- [x] Source audit confirms no installer/runtime PowerShell execution.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.3.4.4`.
- [x] ESET scans the unpacked service (219 objects) and tray (491 objects) with zero detections.
- [x] Inno Setup creates `DeskPulse_Setup_0.3.4.4.exe`.
- [x] ESET scans the installer and all 728 embedded objects with zero detections.
- [x] The clean installer is copied to `releases\current`.
- [x] The live installer completes with exit code zero.
- [x] Installed service and tray hashes match the published 0.3.4.4 binaries.
- [x] Installed service reports Running and Automatic; the installed tray is running as 0.3.4.4.
- [x] The all-users Startup shortcut exists and no DeskPulse scheduled startup task remains.
- [x] ESET scans both installed executables (710 objects) with zero detections.

## Existing architecture acceptance

- [x] ProgramData migration, ACL, schema and SQLite integrity passed during 0.3.2.x acceptance.
- [x] SID/session routing and simultaneous-user isolation passed during 0.3.2.x acceptance.
- [x] Named-pipe authorization and system/current-user maintenance boundaries passed.
- [x] Current-user and system log/settings process isolation passed.

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database. Rule-candidate diagnostics store bounded aggregate process names and file extensions only.
