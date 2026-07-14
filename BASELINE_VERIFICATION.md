# DeskPulse 0.2.0.1 Baseline Verification

This package is locked as the authoritative DeskPulse 0.2.0.1 source and handover baseline, subject to the user's final local compilation and acceptance testing.

Verified in the source package:

- Active application version constant: 0.2.0.1
- Shared, Service and Tray project versions: 0.2.0.1
- Installer version and output filename: 0.2.0.1
- Publish folders: `publish\v0.2.0.1\service` and `publish\v0.2.0.1\tray`
- README, HANDOVER, ROADMAP, VERSION_CHECK, GITHUB_RELEASE and current CHANGELOG release: 0.2.0.1
- GitHub repository: `https://github.com/KaiEysselein/DeskPulse`
- Historical version references are confined to release history and backward-compatibility comments.
- Tray source contains no direct SQLite write mode or SQL INSERT, UPDATE, DELETE or VACUUM statements.
- `DATABASE_WRITE_AUDIT.md` records the service-owned database-write boundary.

Final release remains conditional on successful local execution of:

```powershell
.\scripts\Build.ps1
.\scripts\Publish.ps1
.\Installer\Build-Installer.ps1
```

and the acceptance checklist in `VERSION_CHECK.md`.

## Selected-record deletion stability

- Fixed selected-record deletion waiting indefinitely: service write commands now use the monitor-owned database instance and shared database lock instead of pausing ETW and opening a competing database instance.
- View Log deletion now awaits the service asynchronously so the form remains responsive.
