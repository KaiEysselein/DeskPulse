# DeskPulse 0.4.0.3

DeskPulse 0.4.0.3 completes the integrated Records and Calendar workflow and improves feedback while large datasets are processed.

## Highlights

- Uses one destination-labelled **Records/Calendar** toggle inside both Current User Log and elevated System Log.
- Removes the standalone Calendar tray-menu commands.
- Adds a remembered **All records/Marked records** Calendar filter.
- Supports double-click Calendar grouping and ungrouping by date, hour, activity or item.
- Supports expandable and collapsible Calendar group rows.
- Carries a selected Calendar date into the Records layout.
- Moves Export to the top toolbar and Records per page to the far left of paging controls.
- Moves the 24-hour/AM-PM preference into Current User Settings.
- Adds a per-user, three-second startup status message with an installer opt-out.
- Adds animated progress feedback for loading, paging, sorting, grouping, expansion, collapse and filter changes.

## Upgrade

The installer preserves databases, administrator rule overrides and existing per-user preferences. The startup-message installer choice is applied on fresh installations only, so upgrades do not reset the user's saved preference.

## Installer

```text
DeskPulse_Setup_0.4.0.3.exe
```

SHA-256:

```text
7F1F881F99C0CE5BF4F7419EC256E08BC5C20FF3C15C4DF8EBBF3DDFBE34A815
```

## Verification

The Release build, 73 automated tests, packaging, installation, binary hashes and live service/tray verification are recorded in `VERSION_CHECK.md`.
