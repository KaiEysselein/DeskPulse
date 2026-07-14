# GitHub update — DeskPulse 0.2.0.1

Repository: https://github.com/KaiEysselein/DeskPulse

## Commit

```text
Release DeskPulse 0.2.0.1 stabilisation baseline
```

## Release

```text
Tag: v0.2.0.1
Title: DeskPulse 0.2.0.1 — Stabilisation Baseline
```

## Release notes

DeskPulse 0.2.0.1 locks the current Windows service and tray architecture as the stabilisation baseline.

Highlights:

- Background ETW, application and Windows-session monitoring runs in `DeskPulse.Service`; the normal tray remains non-elevated.
- All SQLite mutations are service-owned, including selected-record deletion, Create Rule cleanup, Database Housekeeping, clear-table actions, migrations and `VACUUM`.
- The tray opens the database read-only for views, counts, statistics and exports, resolving the SQLite read-only failures seen during maintenance and deletion.
- Added **Track Windows system activity**, disabled by default, with visible read-only Windows-default exclusions covering the complete `%WINDIR%` tree, selected ProgramData locations, the Recycle Bin and high-volume system processes.
- Tray autostart uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `DeskPulse.Tray`; the service starts independently as an automatic Windows service.
- Legacy relative data paths are converted to absolute paths under the interactive user's Documents folder.
- Publish output is versioned under `publish\v0.2.0.1\service` and `publish\v0.2.0.1\tray`.
- The installer is self-contained for x64 Windows and uninstall preserves `%USERPROFILE%\Documents\DeskPulse`.

## Release assets

- `DeskPulse_Setup_0.2.0.1.exe`
- `DeskPulse_0.2.0.1_Handover.zip`

The installer output, publish folders, binaries, databases and ZIP packages should remain excluded from the Git repository and be attached to the GitHub Release where applicable.

## GitHub Desktop workflow

1. Run the full local build, publish, installer and acceptance checks in `VERSION_CHECK.md`.
2. Open the DeskPulse repository in GitHub Desktop.
3. Review all changed source and documentation files.
4. Confirm `publish`, `bin`, `obj`, databases, ZIPs and EXEs are not staged.
5. Commit with the message above and push to `main`.
6. Create GitHub release tag `v0.2.0.1`.
7. Paste the release notes above and attach the installer and handover ZIP.
8. Publish the release.
