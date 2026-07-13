# GitHub update — DeskPulse 0.2.0.0

Repository: https://github.com/KaiEysselein/DeskPulse

## Recommended commit message

```text
Release DeskPulse 0.2.0.0 service and tray architecture
```

## Recommended release tag

```text
v0.2.0.0
```

## Release title

```text
DeskPulse 0.2.0.0 — Windows Service and Tray Split
```

## Release notes

DeskPulse 0.2.0.0 introduces a Windows service and non-elevated tray architecture.

Highlights:

- Background ETW, application and Windows-session monitoring now runs in `DeskPulse.Service`.
- The tray UI no longer requires elevation and can be closed without stopping monitoring.
- Added Pause Logging / Resume Logging.
- Added service status and Maintenance → Restart Windows Service.
- Added a self-contained x64 installer that registers the service and starts the tray at login.
- Uninstall removes the application, service, startup entries and settings while preserving the database in `Documents\DeskPulse`.
- Added full-result sorting, precise paging ranges, earliest-record report defaults and Today Only.
- Added optional rule creation on deletion and startup/login/logout/lock/unlock activity logging.

## Files to attach to the GitHub release

- `DeskPulse_Setup_0.2.0.0.exe`
- `DeskPulse_0.2.0.0_Handover.zip`

The installer output is ignored by `.gitignore`, which is appropriate: attach it to the GitHub Release rather than committing the binary to the repository.

## GitHub Desktop workflow

1. Open the DeskPulse repository in GitHub Desktop.
2. Review all changed source and documentation files.
3. Confirm that `publish`, `bin`, `obj`, databases, ZIPs and EXEs are not staged.
4. Commit with the recommended commit message.
5. Push to `main`.
6. On GitHub, create a new release using tag `v0.2.0.0`.
7. Paste the release notes above.
8. Attach the installer and handover ZIP.
9. Publish the release.
