# DeskPulse 0.3.1.0 Final Release Audit

## Status

Approved after successful local build, installation and acceptance verification on 2026-07-22.

## Release identity

- Application and assembly version: 0.3.1.0
- Installer: `DeskPulse_Setup_0.3.1.0.exe`
- Publish root: `dev\publish\v0.3.1.0`
- Permanent release folder: `releases\v0.3.1.0`
- GitHub tag: `v0.3.1.0`

## Scope

This release promotes the completed 0.3.0.1 correction work and includes:

- stable database-cleanup confirmation behaviour;
- installation lifecycle User Activity logging;
- corrected transparent tray-state assets;
- enabled migration and logging for diagnostic-load, resource-warning and critical-safety events;
- phase-staggered diagnostic CPU load with headroom below the 50% hard cap;
- release and GitHub documentation updated for 0.3.1.0.

## Acceptance evidence

- Upgrade from 0.3.0.1 and same-version reinstall passed.
- Service/tray version, automatic service startup and single-tray-instance checks passed.
- File, App and User Activity, cleanup and SQLite-integrity checks passed.
- Normal, Paused and Warning icon-transparency checks passed.
- Warning, critical pause, restart persistence, resume recovery and manual diagnostic cancellation passed.
- Final CPU-cap test peaked at 48.1% visually and 48.2% in DeskPulse's warning record.
- Required publish and retained installer outputs exist and match byte-for-byte.

Historical verification and release-history documents remain unchanged.
