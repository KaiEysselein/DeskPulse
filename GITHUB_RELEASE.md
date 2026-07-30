# DeskPulse 0.4.0.5

DeskPulse 0.4.0.5 makes every Records activity table collapsible and summarised.

## Highlights

- File, App and User Activity tables all support collapsible grouped rows.
- User Activity can be grouped by date, event, user or computer.
- User groups support expansion, summary details, Calendar marking and grouped deletion.
- Regression tests cover every supported User Activity grouping header.

## Upgrade

The installer preserves databases, administrator rule overrides and existing per-user preferences. The startup-message installer choice is applied on fresh installations only, so upgrades do not reset the user's saved preference.

## Installer

```text
DeskPulse_Setup_0.4.0.5.exe
```

SHA-256:

```text
AA3BC8DA12B06E49EAEACD3B0A9FC87860169C2D458ECAB109BAE42AD06A3337
```

## Verification

The Release build, 79 automated tests, packaging, installation, binary hashes and live database integrity verification are recorded in `VERSION_CHECK.md`.
