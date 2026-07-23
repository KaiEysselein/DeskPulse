# DeskPulse 0.3.3.0

DeskPulse 0.3.3.0 completes the service-owned system/per-user storage, attribution and security architecture introduced after 0.3.2.0.

## Highlights

- Protected system and per-user databases and settings under `%ProgramData%\DeskPulse`.
- SID- and session-attributed file, application and user activity with simultaneous-session routing.
- Service-side authorization for state-changing named-pipe commands.
- **Current User** Log, Settings and Maintenance isolated from UAC-elevated System Log, Settings and Maintenance.
- No combined all-users log and no administrator access path into another user's personal database.
- Complete date-range exports with progress, independent of display-page size.
- Optional suppression of folder-opening events.
- One DeskPulse form open from the tray at a time.

## Upgrade

The installer supports upgrading an existing DeskPulse installation. Legacy data is backed up, migrated with SQLite online backup and integrity validation, and retained for rollback.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Administrators can review and maintain only the system database; each user can access only their SID database.

## Installer

Release asset:

```text
DeskPulse_Setup_0.3.3.0.exe
```

## Verification

The 0.3.2.x storage, routing, authorization, settings, UI and export slices were interactively verified on 2026-07-23. The final 0.3.3.0 build and packaging results are recorded in `VERSION_CHECK.md`; the newest single-window guard remains pending a short runtime confirmation.
