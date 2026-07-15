# Version Check — 0.2.2.0

Version **0.2.2.0** is the locked active baseline. Historical version numbers in `CHANGELOG.md` are intentionally preserved.

## Verified active references

- `DeskPulse.Shared` AppInfo: `0.2.2.0`
- `DeskPulse.Shared.csproj`: `0.2.2.0`
- `DeskPulse.Service.csproj`: `0.2.2.0`
- `DeskPulse.Tray.csproj`: `0.2.2.0`
- Inno Setup installer and output filename: `0.2.2.0`
- Publish folders: `publish\v0.2.2.0\service` and `publish\v0.2.2.0\tray`
- README, HANDOVER, ROADMAP, GITHUB_RELEASE and current CHANGELOG entry: `0.2.2.0`
- GitHub repository: `https://github.com/KaiEysselein/DeskPulse`
- Pipe ACL package: `System.IO.Pipes.AccessControl` `5.0.0`

## Deployment checks

- [ ] `Build.ps1` completes with zero errors.
- [ ] `Publish.ps1` creates both self-contained executables under `publish\v0.2.2.0`.
- [ ] `Build-Installer.ps1` creates `publish\v0.2.2.0\installer\DeskPulse_Setup_0.2.2.0.exe`.
- [ ] `DeskPulse.Service` starts automatically after reboot.
- [ ] One `DeskPulse.Tray` process starts through the per-user `DeskPulse.Tray` HKCU Run value.
- [ ] Legacy scheduled tasks and Startup-folder shortcuts do not create duplicate trays.
- [ ] Shared settings contain absolute Documents-based data paths.
- [ ] View Log, selected-record deletion, Create Rule cleanup, Maintenance housekeeping and clear-table operations complete without SQLite read-only errors.
- [ ] The tray opens SQLite read-only; all database mutations are executed by the service.
- [ ] Under General → Activity logging, **Track Windows system activity** is unchecked by default.
- [ ] **Configure filtered file applications...** opens the dual-list process filter and loads distinct applications from File Activity history.
- [ ] Applications moved to the filtered list are excluded from new File Activity logging case-insensitively.
- [ ] Rule-based database housekeeping removes historical File Activity records attributed to currently filtered applications.
- [ ] With system tracking off, `%WINDIR%\**` and the other built-in Windows-default exclusions are not logged.
- [ ] Housekeeping applies the same Windows-default policy to historical records.
- [ ] Uninstall removes application, service, startup registrations and shared settings while preserving `%USERPROFILE%\Documents\DeskPulse`.

## Settings footer acceptance

- General: Save and Cancel visible; Import/Export hidden.
- Rules: Save, Cancel, Import Rules and Export Rules visible.
- Export Options tab removed; standard export remains available from the log viewer.
- Maintenance: Save hidden; one Close button visible and functional after housekeeping and service restart.
- Activity logging settings and the filtered-application list persist after pressing Save or Save and Close.

- Rules Import offers **Merge with existing rules** (default) or **Replace existing rules**. Merge updates matching File/App rules and adds new ones without duplicates; User Activity rules are unchanged.
