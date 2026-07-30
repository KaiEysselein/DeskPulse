# DeskPulse 0.4.0.4

DeskPulse 0.4.0.4 corrects and extends Calendar activity display and summarisation.

## Highlights

- Calendar View includes File, App and User Activity.
- App and User rows use useful item and detail fallbacks.
- Double-clicking the Details header groups or ungroups records and displays record totals.
- Regression tests execute the production Calendar query against all three activity tables.

## Upgrade

The installer preserves databases, administrator rule overrides and existing per-user preferences. The startup-message installer choice is applied on fresh installations only, so upgrades do not reset the user's saved preference.

## Installer

```text
DeskPulse_Setup_0.4.0.4.exe
```

SHA-256:

```text
48C088B54BE9F49EEB53EB18BC62A8C2964AA1B5BA91EDB4FECD1BB172F11928
```

## Verification

The Release build, 75 automated tests, packaging, installation, binary hashes and live database integrity verification are recorded in `VERSION_CHECK.md`.
