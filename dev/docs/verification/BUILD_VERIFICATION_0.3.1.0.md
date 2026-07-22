# DeskPulse 0.3.1.0 Build Verification

## Status

Passed local Windows build, installation and acceptance verification on 2026-07-22.

## Required outputs

- `publish\v0.3.1.0\service\DeskPulse.Service.exe`
- `publish\v0.3.1.0\tray\DeskPulse.Tray.exe`
- `publish\v0.3.1.0\installer\DeskPulse_Setup_0.3.1.0.exe`
- `releases\current\DeskPulse_Setup_0.3.1.0.exe`
- `releases\v0.3.1.0\DeskPulse_Setup_0.3.1.0.exe`

## Verification record

- Release build succeeded with zero errors for Shared, Service and Tray.
- Publish succeeded for the self-contained win-x64 service and tray executables.
- Inno Setup 6.7.3 compiled `DeskPulse_Setup_0.3.1.0.exe` successfully.
- Installed service and tray both report file version 0.3.1.0.
- Upgrade from installed 0.3.0.1 to 0.3.1.0 passed.
- Same-version 0.3.1.0 reinstall lifecycle logging passed.
- Exactly one responsive tray instance remained after diagnostic helper windows closed.
- DeskPulse.Service remained running with Automatic startup.
- File, App and User Activity logging continued normally after upgrade and cleanup.
- Database cleanup displayed confirmation and completion, preserved Settings and left SQLite integrity `ok`.
- Installation update and reinstall each wrote one correct User Activity lifecycle record.
- Normal, Paused and Warning tray icons displayed with transparent backgrounds.
- Warning diagnostic recorded one resource warning while logging remained active.
- Critical diagnostic recorded a safety-pause event and created the persistent pause marker.
- Critical safety pause survived a Windows service restart.
- Resume Logging removed the marker and restored File/App activity.
- Diagnostic-load, warning and critical events migrated into enabled User Activity rules.
- Phase-staggered 50% CPU test peaked at 48.1% visually and 48.2% in the recorded warning sample.
- Stop Test recorded both cancellation-requested and cancelled diagnostic events.
- The three installer copies were byte-identical after final generation.
- Final installer SHA-256: `24125D7FC5DC93DCA1A82EFAADDF1B367213B6EF0D08A303889D88CBC01407A4`.
