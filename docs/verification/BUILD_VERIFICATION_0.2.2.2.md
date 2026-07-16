# DeskPulse 0.2.2.2 Build Verification

## Scope

Cleanup and housekeeping release based on the authoritative 0.2.2.1 source handover.

## Source checks completed

- Active application, assembly, installer and publish references use 0.2.2.2.
- Service and Tray icon assets are named `DeskPulse.ico`.
- No active `file-logger.ico` asset or reference remains.
- The solution contains only DeskPulse.Service, DeskPulse.Shared and DeskPulse.Tray.
- Historical verification and audit documents are organized under `docs\archive`.
- Current verification documents are organized under `docs\verification`.
- Generated build, publish, installer output, database and log files are excluded by `.gitignore`.

## Local verification required

- Run `scripts\Build.ps1`.
- Run `scripts\Publish.ps1`.
- Run `Installer\Build-Installer.ps1`.
- Install and launch the generated 0.2.2.2 installer.
- Confirm the service and tray start correctly and existing 0.2.2.1 behaviour remains unchanged.

The supplied preparation environment did not contain the .NET SDK or Inno Setup, so compilation and installer generation must be completed on the user's Windows development machine.
