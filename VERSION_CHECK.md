# Version Check — 0.3.0.0

Version **0.3.0.0** is the locked active milestone baseline. Historical version numbers in `CHANGELOG.md` and archived verification records are intentionally preserved.

## Verified active references

- `DeskPulse.Shared` AppInfo: `0.3.0.0`
- `DeskPulse.Shared.csproj`: `0.3.0.0`
- `DeskPulse.Service.csproj`: `0.3.0.0`
- `DeskPulse.Tray.csproj`: `0.3.0.0`
- Inno Setup installer and output filename: `0.3.0.0`
- Development root: `dev`
- Solution: `dev\DeskPulse.sln`
- Publish folders: `dev\publish\v0.3.0.0\service` and `dev\publish\v0.3.0.0\tray`
- Installer: `dev\publish\v0.3.0.0\installer\DeskPulse_Setup_0.3.0.0.exe`
- Permanent milestone folder: `releases\v0.3.0.0`
- GitHub tag: `v0.3.0.0`
- Root README, HANDOVER, ROADMAP, GITHUB_RELEASE, VERSION_CHECK, and current CHANGELOG entry: `0.3.0.0`
- GitHub repository: `https://github.com/KaiEysselein/DeskPulse`

## Deployment checks

- [ ] From `dev`, `scripts\Build.ps1` completes with zero errors.
- [ ] `dev\scripts\Publish.ps1` creates both self-contained executables under `dev\publish\v0.3.0.0`.
- [ ] `dev\Installer\Build-Installer.ps1` creates `DeskPulse_Setup_0.3.0.0.exe`.
- [ ] The installer is copied to `releases\current` and `releases\v0.3.0.0`.
- [ ] `DeskPulse.Service` starts automatically after reboot.
- [ ] Exactly one tray process starts for the configured user through the per-user HKCU Run value.
- [ ] File, App, and User Activity logging operate normally.
- [ ] View Log, deletion, Create Rule cleanup, Maintenance housekeeping, and clear-table operations complete without SQLite read-only errors.
- [ ] The tray opens SQLite read-only; all database mutations are executed by the service.
- [ ] Warning and critical CPU/RAM thresholds and sustained durations save and reload correctly.
- [ ] Diagnostic CPU, RAM, and combined tests never exceed the service-side 50% cap.
- [ ] The live diagnostic window shows progress and measured values and **Stop Test** cancels the test.
- [ ] Sustained warning state logs one warning and allows logging to continue.
- [ ] Sustained critical state pauses logging and shows the safeguard state.
- [ ] With persistent post-trigger pause enabled, the safety pause survives service and Windows restarts.
- [ ] **Resume Logging** clears the critical marker and restarts monitoring.
- [ ] Uninstall removes application, service, startup registrations, and shared settings while preserving `%USERPROFILE%\Documents\DeskPulse`.

## Known deferred item

- Tray-state icon transparency remains a visual correction for a later build.
