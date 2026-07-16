# DeskPulse Feature Backlog

This file records work intentionally deferred beyond the accepted `0.3.0.0` milestone.

## Multi-user tray startup

- Add an installer option to start `DeskPulse.Tray` for every Windows user on the machine.
- Prefer a machine-wide **At logon of any user** scheduled task over a simple HKLM Run entry.
- Resolve concurrent-session behaviour and prevent duplicate tray instances within each user session.
- Confirm whether settings and database ownership remain shared or require per-user separation.
- Preserve the automatic machine-wide Windows service independently of tray startup.

## Tray icon transparency

- Rebuild Normal, Paused, and Warning ICO resources with true alpha transparency at all required Windows icon sizes.
- Verify appearance on light and dark Windows themes and at common tray scaling levels.

## Service and diagnostics refinements

- Add structured and versioned named-pipe request/response contracts.
- Expand service-health, reconnect, and diagnostic-history views.
- Add installer logging and clearer upgrade/recovery diagnostics.

## Data protection and maintenance

- Add automated database backup and restore controls.
- Add optional retention policies for very large databases.
- Consider code signing for release binaries and installers.
- Add an automated GitHub build and release workflow.
