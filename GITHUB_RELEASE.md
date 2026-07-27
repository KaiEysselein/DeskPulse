# DeskPulse 0.3.3.1

DeskPulse 0.3.3.1 is a patch release for standalone Log and Settings window visibility.

## Highlights

- Current User Log and Settings now fit within the active screen's working area.
- UAC-elevated System Log and System Settings receive the same visibility correction.
- Standalone windows are centered, restored, activated and brought to the foreground after opening.
- Compact screens and high display scaling no longer leave these windows accessible only through a taskbar icon.

## Upgrade

The installer supports upgrading an existing DeskPulse installation. Legacy data is backed up, migrated with SQLite online backup and integrity validation, and retained for rollback.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Administrators can review and maintain only the system database; each user can access only their SID database.

## Installer

Release asset:

```text
DeskPulse_Setup_0.3.3.1.exe
```

## Verification

The 0.3.3.1 release build, packaging and installed executable verification are recorded in `VERSION_CHECK.md`.
