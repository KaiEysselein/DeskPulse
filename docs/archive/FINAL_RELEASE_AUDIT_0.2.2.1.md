# DeskPulse 0.2.2.1 Final Release Audit


Tray-opened forms close automatically after external focus loss, and log views support a persisted 24-hour or 12-hour AM/PM time display.
## Source/package checks completed

- Active source, project, installer and publish references use 0.2.2.1.
- The GitHub-ready release was promoted from the audited 0.2.1.8 baseline without changing its implemented feature set.
- Historical versions remain confined to changelog and historical verification documents.
- File Activity contains a visible Activity column and Activity grouping.
- View Log strips fractional seconds from displayed File/App/User times.
- The Settings runtime tab list excludes the Export Options tab.
- Filtered File Activity processes remain case-insensitive and participate in historical housekeeping.
- Mapped-drive normalization remains enabled for new records.
- `.gitignore` excludes build output, publish output, runtime databases, ZIPs and installer executables.
- Build and publish scripts now check native command exit codes and expected publish executables.

## Local release gate

The package was audited structurally in a non-Windows environment. Before tagging the release, the Windows build must complete successfully and the installer must be tested. Complete the checklist in `VERSION_CHECK.md`.
