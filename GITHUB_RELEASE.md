# DeskPulse 0.4.0.0

DeskPulse 0.4.0.0 promotes Calendar View to a first-class tray workflow for both current-user and protected system activity.

## Highlights

- File, App and User Activity records now have a persistent Calendar checkbox.
- Collapsed File and App Activity groups support checked, unchecked and mixed Calendar states; changing a group updates every underlying record in the selected report period.
- Calendar View lists all marked records, highlights dates containing marks and filters to a selected day.
- Calendar updates run through the authenticated DeskPulse service, preserving protected database permissions and safely batching large groups.
- The tray menu provides **Current User → Calendar View** and elevated **Administrator → System Calendar View** commands.
- Calendar windows are independent, refresh when activated and return to the foreground when selected again.
- Calendar windows open maximized.
- Activity reports now use single-click column sorting and double-click header grouping, replacing the Group by dropdown.
- Process-token ownership distinguishes SYSTEM and service accounts from interactive users.
- `route_system` and `route_user` policy actions give each event exactly one destination.
- Read-only historical attribution previews propose movements without modifying either database.
- File and App Activity grouping summarizes and sorts the complete date range before pagination.
- App Activity supports expandable groups in Current User and Administrator logs.
- Newly added rules reload before retroactive cleanup and activate immediately.
- Log period presets support exact rolling hours, days and years as well as midnight-based Today and This Month ranges.
- Right-click report cells to delete records or create a pre-populated rule from the clicked file, extension, folder, application or executable path.

## Upgrade

The installer replaces shipped defaults, preserves administrator overrides and existing databases, and protects rule configuration so only LocalSystem and administrators can modify it.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Candidate diagnostics contain no full paths, user names, SIDs or event contents.

## Installer

```text
DeskPulse_Setup_0.4.0.0.exe
```

## Verification

Release build, automated tests, packaging, installation and live verification are recorded in `VERSION_CHECK.md`.
