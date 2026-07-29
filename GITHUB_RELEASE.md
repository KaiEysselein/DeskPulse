# DeskPulse 0.3.4.4

DeskPulse 0.3.4.4 improves System/User attribution, activity grouping, rule creation and cleanup reliability, and removes PowerShell from installation and runtime operations.

## Highlights

- Process-token ownership distinguishes SYSTEM and service accounts from interactive users.
- `route_system` and `route_user` policy actions give each event exactly one destination.
- Read-only historical attribution previews propose movements without modifying either database.
- File and App Activity grouping summarizes and sorts the complete date range before pagination.
- App Activity supports expandable groups in Current User and Administrator logs.
- Newly added rules reload before retroactive cleanup and activate immediately.
- Log period presets support exact rolling hours, days and years as well as midnight-based Today and This Month ranges.

## Upgrade

The installer replaces shipped defaults, preserves administrator overrides and existing databases, and protects rule configuration so only LocalSystem and administrators can modify it.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Candidate diagnostics contain no full paths, user names, SIDs or event contents.

## Installer

```text
DeskPulse_Setup_0.3.4.4.exe
```

## Verification

Release build, automated tests, packaging, installation and live verification are recorded in `VERSION_CHECK.md`.
