# Version Check — 0.4.0.0

Version **0.4.0.0** is the current milestone release. Historical version numbers in changelog entries and archived verification records are intentionally preserved.

## Active references

- `DeskPulse.Shared` AppInfo: `0.4.0.0`
- Shared, service and tray project versions: `0.4.0.0`
- Inno Setup installer and output filename: `0.4.0.0`
- Publish folders: `dev\publish\v0.4.0.0\service` and `dev\publish\v0.4.0.0\tray`
- Installer: `dev\publish\v0.4.0.0\installer\DeskPulse_Setup_0.4.0.0.exe`
- Current approved installer folder: `releases\current`
- Retained milestone: `releases\v0.4.0.0`
- GitHub tag target: `v0.4.0.0`
- Installer SHA-256: `A0476960D306253FAA9BA73051A96A385DF4BCBA90E15CDF749C045CFA8E9A21`

## 0.4.0.0 release checks

- [x] Full Release build completes with zero warnings and zero errors.
- [x] All 69 automated tests pass.
- [x] Source audit confirms no installer/runtime PowerShell execution.
- [x] Self-contained service and tray outputs exist under `dev\publish\v0.4.0.0`.
- [x] ESET scans the unpacked service (221 objects) and tray (499 objects) with zero detections.
- [x] Inno Setup creates `DeskPulse_Setup_0.4.0.0.exe`.
- [x] ESET scans the installer and all 728 embedded objects with zero detections.
- [x] The clean installer is copied to `releases\current`.
- [x] The live installer completes successfully.
- [x] Installed service and tray hashes match the published 0.4.0.0 binaries.
- [x] Installed service reports Running and Automatic; the installed tray is running as 0.4.0.0.
- [x] The all-users Startup shortcut exists and no DeskPulse scheduled startup task remains.
- [x] ESET scans both installed executables (710 objects) with zero detections.

## Existing architecture acceptance

- [x] ProgramData migration, ACL, schema and SQLite integrity passed during 0.3.2.x acceptance.
- [x] SID/session routing and simultaneous-user isolation passed during 0.3.2.x acceptance.
- [x] Named-pipe authorization and system/current-user maintenance boundaries passed.
- [x] Current-user and system log/settings process isolation passed.

## Scope boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected system database. Rule-candidate diagnostics store bounded aggregate process names and file extensions only.
