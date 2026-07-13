# DeskPulse Roadmap

## Current baseline: 0.2.0.0

The service/tray split, installer, pause/resume control, service restart control and core Log View improvements are implemented.

## 0.2.x priorities

- Complete clean-PC installation, upgrade, restart and uninstall regression testing.
- Move all tray-side database mutations behind service IPC so the service becomes the sole database writer.
- Add structured and versioned named-pipe request/response contracts.
- Add service health, reconnect and diagnostics views.
- Add installer logging and clearer upgrade/recovery diagnostics.
- Review multi-user Windows behaviour and per-user tray startup.
- Add automated database backup/restore controls.

## Later

- Code-sign release binaries and installer.
- Add automated build/release workflow for GitHub.
- Add optional retention policies and performance safeguards for very large databases.
