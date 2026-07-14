# DeskPulse Roadmap

## Current baseline: 0.2.0.1

The service/tray split, installer, pause/resume control, service restart control, Windows-system activity controls, and service-owned SQLite write architecture are implemented.

## Baseline status

Version 0.2.0.1 is locked for compilation, installation and acceptance testing. New feature work should use the next development version.

## 0.2.x priorities

- Complete clean-PC installation, upgrade, restart and uninstall regression testing.
- Add structured and versioned named-pipe request/response contracts.
- Add service health, reconnect and diagnostics views.
- Add installer logging and clearer upgrade/recovery diagnostics.
- Review multi-user Windows behaviour and per-user tray startup.
- Add automated database backup/restore controls.

## Later

- Code-sign release binaries and installer.
- Add automated build/release workflow for GitHub.
- Add optional retention policies and performance safeguards for very large databases.
