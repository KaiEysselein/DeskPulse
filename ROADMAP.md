# DeskPulse Roadmap

## Current baseline: 0.2.2.0

Completed in 0.2.2.0:

- Visible File Activity **Activity** column and Activity grouping.
- Whole-second time display in log tables.
- Removal of the user-facing Export Options tab while retaining standard export.
- Configurable File Activity process filtering with historical cleanup support.
- Automatic mapped-drive normalization for newly logged records.

## Baseline status

Version 0.2.2.0 is locked for local compilation, installation and acceptance testing. New feature work should use the next development version.

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
