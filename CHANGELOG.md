# Changelog

All notable DeskPulse changes are recorded here. Historical verification records under `dev\docs` remain unchanged.

## 0.3.1.0 — 2026-07-20

### Added

- User Activity records for first installation, version update and same-version reinstallation.
- Release verification documents for 0.3.1.0.

### Fixed

- **Clean database with current rules...** no longer causes the Settings window to disappear before confirmation.
- Settings is no longer closed by the generic tray focus-loss mechanism.
- Normal, Paused and Warning tray icons now use genuine transparent PNG and ICO assets without checkerboard or rectangular backgrounds.

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
