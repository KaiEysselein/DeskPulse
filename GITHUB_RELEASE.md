# GitHub update — DeskPulse 0.2.2.1


Tray-opened forms close automatically after external focus loss, and log views support a persisted 24-hour or 12-hour AM/PM time display.
Repository: https://github.com/KaiEysselein/DeskPulse

## Commit

```text
Release DeskPulse 0.2.2.1 stabilisation baseline
```

## Release

```text
Tag: v0.2.2.1
Title: DeskPulse 0.2.2.1 — File Activity improvements
```

## Release notes

DeskPulse 0.2.2.1 improves File Activity review and completes the configurable application-filter workflow introduced in 0.2.1.7.

Highlights:

- Added a visible **Activity** column to File Activity, preferring the inferred user-facing action and falling back to the recorded activity type.
- Added **Activity** to File Activity grouping.
- File/App/User log times display at whole-second precision; database timestamps retain their original precision.
- Removed the user-facing **Export Options** tab while retaining standard Excel export behaviour.
- Retained configurable filtered File Activity applications and historical cleanup support.
- Retained automatic mapped-drive normalization for newly logged `LanmanRedirector` paths.
- Build and publish scripts now fail immediately when a native `dotnet` command fails and verify that expected executables exist before reporting success.
- Publish output is versioned under `publish\v0.2.2.1\service` and `publish\v0.2.2.1\tray`.
- The installer is self-contained for x64 Windows.

## Release assets

- `DeskPulse_Setup_0.2.2.1.exe`
- `DeskPulse_0.2.2.1_Source_Handover.zip`

Generated binaries, publish folders, databases, ZIP packages and installer executables should remain excluded from the Git repository and be attached to the GitHub Release where applicable.

## GitHub Desktop workflow

1. Run the full local build, publish, installer and acceptance checks in `VERSION_CHECK.md`.
2. Review all changed source and documentation files in GitHub Desktop.
3. Confirm `publish`, `bin`, `obj`, databases, ZIPs and EXEs are not staged.
4. Commit with the message above and push to `main`.
5. Create tag `v0.2.2.1`, attach the installer and handover ZIP, and publish the release.
