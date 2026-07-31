# DeskPulse 0.4.0.6

DeskPulse 0.4.0.6 refines the integrated Calendar workflow and makes installer upgrades quieter and safer.

## Highlights

- Calendar now has separate **Files**, **Apps** and **User Activity** tabs.
- Each tab displays and exports only its own activity type.
- Calendar loads only records explicitly marked for Calendar view.
- Grouping and expanded/collapsed state are maintained independently for each tab.
- The cross-tab collapse-state bug that caused erratic behaviour has been fixed.
- The obsolete All records/Marked records toggle and saved preference have been removed.
- The installer no longer shows an Additional Tasks page.
- Fresh installs enable the desktop shortcut and startup message by default.
- Upgrades and same-version reinstalls preserve the user's existing choices.
- The automated suite now contains **81 passing tests**.

## Upgrade

Run the installer over an existing DeskPulse installation. Databases, administrator rule overrides and per-user preferences are preserved.

## Installer

```text
DeskPulse_Setup_0.4.0.6.exe
```

SHA-256:

```text
413A8E7B4C3C8002BF8D582C4C93F4BB090832CD99A87DA32FAA8D681588E1AE
```

## Verification

Build, test, publish, installer and installed runtime acceptance details are recorded in [`VERSION_CHECK.md`](VERSION_CHECK.md).

