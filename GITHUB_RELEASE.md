# DeskPulse 0.3.3.2

DeskPulse 0.3.3.2 locks in the tested tray-menu, export-progress and pause-icon fixes before Calendar development begins.

## Highlights

- Dedicated Excel-export progress windows for Current User and System Log.
- Log closes after a successful export while remaining open on cancellation or failure.
- Reliable first-click tray-menu dispatch after the menu closes.
- Windows 11 hidden-icons flyout no longer covers the DeskPulse menu.
- Normal, Paused and Warning runtime icons are packaged and installed correctly.

## Upgrade

The installer supports upgrading an existing DeskPulse installation. Legacy data is backed up, migrated with SQLite online backup and integrity validation, and retained for rollback.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Administrators can review and maintain only the system database; each user can access only their SID database.

## Installer

Release asset:

```text
DeskPulse_Setup_0.3.3.2.exe
```

## Verification

The 0.3.3.2 release build, packaging, installation and manual acceptance are recorded in `VERSION_CHECK.md`.
