# DeskPulse 0.2.1.7 Build Verification

## Scope

- Explorer-attributed file activity setting added under General → Activity logging.
- Track Windows system activity moved from Maintenance to General.
- Historical Repair Data maintenance UI removed.
- Active version references advanced to 0.2.1.7.

## Source-level checks completed

- AppSettings includes `LogExplorerFileActivity`, defaulting to `true`, and preserves it during cloning and saving.
- Live file monitoring suppresses `explorer` and `explorer.exe` case-insensitively when the setting is disabled.
- Database housekeeping applies the same setting to historical File Activity records.
- Maintenance runtime page contains Database housekeeping and Windows service only.

## Local verification required

The .NET SDK is unavailable in the packaging environment. Run `scripts\Build.ps1`, `scripts\Publish.ps1`, and `Installer\Build-Installer.ps1` locally and verify the installed UI and service behavior.

## 0.2.1.7 source checks

- Dual-list filtered-application dialog added to the Tray project.
- Available process names are queried from distinct `ActivityEvents.ProcessName` values and aggregated case-insensitively.
- Filtered process settings are persisted and evaluated before File Activity insertion.
- Rule-based database housekeeping evaluates the same filtered-process list against historical File Activity records.
- Local .NET compilation remains required.
