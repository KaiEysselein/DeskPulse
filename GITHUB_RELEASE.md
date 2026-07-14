# GitHub update — DeskPulse 0.2.1.2

Repository: https://github.com/KaiEysselein/DeskPulse

## Commit

```text
Release DeskPulse 0.2.1.2 stabilisation baseline
```

## Release

```text
Tag: v0.2.1.2
Title: DeskPulse 0.2.1.2 — Stabilisation Baseline
```

## Release notes

DeskPulse 0.2.1.2 is a corrective build that standardizes Settings save/close behavior, fixes first-click tray menu activation, and streams detailed database-housekeeping progress from the Windows service.

Highlights:

- Settings now provides **Save**, **Save and Close**, and **Close**, with unsaved-change protection on Close/X/Esc.
- Tray commands open on the first click, restore existing forms, and use **Quit DeskPulse**.
- Housekeeping shows live service-side File/App scan counts and deletion progress at 10% thresholds.
- Background ETW, application and Windows-session monitoring runs in `DeskPulse.Service`; the normal tray remains non-elevated.
- All SQLite mutations are service-owned, including selected-record deletion, Create Rule cleanup, Database Housekeeping, clear-table actions, migrations and `VACUUM`.
- The tray opens the database read-only for views, counts, statistics and exports, resolving the SQLite read-only failures seen during maintenance and deletion.
- Added **Track Windows system activity**, disabled by default, with visible read-only Windows-default exclusions covering the complete `%WINDIR%` tree, selected ProgramData locations, the Recycle Bin and high-volume system processes.
- Tray autostart uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `DeskPulse.Tray`; the service starts independently as an automatic Windows service.
- Legacy relative data paths are converted to absolute paths under the interactive user's Documents folder.
- Publish output is versioned under `publish\v0.2.1.2\service` and `publish\v0.2.1.2\tray`.
- The installer is self-contained for x64 Windows and uninstall preserves `%USERPROFILE%\Documents\DeskPulse`.

## Release assets

- `DeskPulse_Setup_0.2.1.2.exe`
- `DeskPulse_0.2.1.2_Handover.zip`

The installer output, publish folders, binaries, databases and ZIP packages should remain excluded from the Git repository and be attached to the GitHub Release where applicable.

## GitHub Desktop workflow

1. Run the full local build, publish, installer and acceptance checks in `VERSION_CHECK.md`.
2. Open the DeskPulse repository in GitHub Desktop.
3. Review all changed source and documentation files.
4. Confirm `publish`, `bin`, `obj`, databases, ZIPs and EXEs are not staged.
5. Commit with the message above and push to `main`.
6. Create GitHub release tag `v0.2.1.2`.
7. Paste the release notes above and attach the installer and handover ZIP.
8. Publish the release.

- Rules Import offers **Merge with existing rules** (default) or **Replace existing rules**. Merge updates matching File/App rules and adds new ones without duplicates; User Activity rules are unchanged.
