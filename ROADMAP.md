# DeskPulse Roadmap

## Current baseline: 0.2.2.2

Version 0.2.2.2 is a cleanup and housekeeping release. It renames the icon asset to `DeskPulse.ico`, removes obsolete single-project remnants, and organizes verification records without intentionally changing runtime behaviour.


## Release retention

- Retain permanent installer archives and GitHub Releases only for milestone versions matching `v0.x.0.0`.
- Replace `releases\current` for each approved intermediate build.
- Preserve the exact internal version number for diagnostics, installers, handovers, and source history.

## 0.2.x priorities

- Complete clean-PC installation, upgrade, restart and uninstall regression testing.
- Add structured and versioned named-pipe request/response contracts.
- Add service health, reconnect and diagnostics views.
- Add installer logging and clearer upgrade/recovery diagnostics.
- Review multi-user Windows behaviour and per-user tray startup.
- Add automated database backup/restore controls.

## Feature backlog

### Pause modes and tray icon states

- **Pause for this session:** temporarily pause logging without persisting the state. Logging resumes after the DeskPulse service or computer restarts. Display a distinct session-paused tray icon.
- **Pause indefinitely:** persist the paused state across service and computer restarts until the user explicitly enables logging. Display a distinct persistent-paused tray icon.
- **Critical-threshold integration:** when the future sustained critical service CPU/RAM threshold is reached, write the diagnostic record and fallback marker, stop or disable logging safely, and enter the persistent pause state. Keep the tray available to explain the condition and require explicit user re-enablement after the cause has been addressed.

## Later

- Code-sign release binaries and installer.
- Add automated build/release workflow for GitHub.
- Add optional retention policies and performance safeguards for very large databases.
