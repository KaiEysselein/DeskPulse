# Changelog

All notable DeskPulse changes are recorded here. Historical verification records under `dev\docs` remain unchanged.

## 0.3.3.0 — 2026-07-23

### Added

- Added protected system and per-user ProgramData databases, settings and rule ownership keyed by Windows SID.
- Added event scope, SID and Windows-session attribution with simultaneous-session routing.
- Added service-side named-pipe client identity, installation-path and elevation authorization.
- Added isolated current-user Log and Settings plus UAC-elevated System Log and System Settings and Maintenance.
- Added an optional **Log folder openings** setting that preserves extensionless-file logging.
- Added complete active-tab/date-range export with progress reporting.

### Changed

- The tray now groups ordinary actions under **Current User** and machine-wide actions under **Administrator**.
- Current-user actions are named **Log...** and **Settings...**, with hover text identifying their scope.
- Only one DeskPulse form may be open from the tray at a time.
- Per-user maintenance targets only the verified caller's SID database; administrator maintenance targets only the protected system database.
- The all-users scheduled tray task supports parallel Windows sessions and uses the installed tray working directory.

### Fixed

- Service startup now succeeds when no interactive console user is present.
- Settings storage saves no longer attempt to rewrite a read-only parent SID-folder ACL.
- Legacy single-user database migration is guarded so only the first SID receives historical data.

## 0.3.2.0 — 2026-07-22

### Added

- Added an **Administrator settings...** tray action that starts the same executable as a separate process through the Windows UAC `runas` flow.
- Added explicit validation that `--administrator-settings` is running with an elevated administrator token.

### Changed

- Ordinary Settings now remains unelevated and exposes only the user-facing General and Rules pages.
- The elevated, short-lived Administrator settings window exposes only Maintenance and ends administrator access when it closes.
- Service-side named-pipe authorization and the ProgramData system/per-user database architecture are deferred to 0.3.2.x; this release does not present the UI split as a complete security boundary.

### Fixed

- View Log now remains open while its native Save dialog and Excel export workflow have focus, preventing export from being cancelled by the tray focus-loss timer.

## 0.3.1.0 — 2026-07-20

### Added

- User Activity records for first installation, version update and same-version reinstallation.
- Release verification documents for 0.3.1.0.

### Fixed

- **Clean database with current rules...** no longer causes the Settings window to disappear before confirmation.
- Settings is no longer closed by the generic tray focus-loss mechanism.
- Normal, Paused and Warning tray icons now use genuine transparent PNG and ICO assets without checkerboard or rectangular backgrounds.
- Diagnostic-load, service-resource-warning and critical-safety-pause events are now enabled and migrated into User Activity rules.
- Diagnostic CPU workers are phase-staggered with 49% duty headroom so the service remains below the advertised 50% hard cap.

### Changed

- Promoted the completed 0.3.0.1 correction work to release version 0.3.1.0.
- Updated application, assembly, publish, installer, retained-release and GitHub documentation references to 0.3.1.0.
- Release builds whose fourth version component is zero are retained under `releases\v<version>`.

## 0.3.0.0 — 2026-07-16

- Promoted the tested 0.2.2.3 service-safeguard baseline.
- Added configurable sustained CPU and RAM warning and critical thresholds.
- Added critical safety pause, optional restart persistence and explicit Resume Logging recovery.
- Added controlled service-load diagnostics with service-side 50% CPU and RAM caps.

## Earlier versions

Earlier release and verification history remains available under `dev\docs\archive` and `dev\docs\verification`.
