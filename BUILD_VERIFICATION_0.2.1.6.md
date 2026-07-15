# DeskPulse 0.2.1.6 Build Verification

## Scope

- Explorer-attributed file activity setting added under General → Activity logging.
- Track Windows system activity moved from Maintenance to General.
- Historical Repair Data maintenance UI removed.
- Active version references advanced to 0.2.1.6.

## Source-level checks completed

- AppSettings includes `LogExplorerFileActivity`, defaulting to `true`, and preserves it during cloning and saving.
- Live file monitoring suppresses `explorer` and `explorer.exe` case-insensitively when the setting is disabled.
- Database housekeeping applies the same setting to historical File Activity records.
- Maintenance runtime page contains Database housekeeping and Windows service only.

## Local verification required

The .NET SDK is unavailable in the packaging environment. Run `scripts\Build.ps1`, `scripts\Publish.ps1`, and `Installer\Build-Installer.ps1` locally and verify the installed UI and service behavior.
