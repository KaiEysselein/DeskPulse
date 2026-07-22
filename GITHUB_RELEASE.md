# DeskPulse 0.3.1.0

DeskPulse 0.3.1.0 promotes the completed correction work from 0.3.0.1 into a formal release.

## Highlights

- Fixed **Clean database with current rules...** so Settings remains open and the confirmation is displayed correctly.
- Added User Activity records when DeskPulse is installed, updated or reinstalled.
- Corrected transparency for the Normal, Paused and Warning tray icons.
- Preserved the existing service CPU/RAM safeguards and their service-side 50% diagnostic caps.
- Enabled User Activity logging for diagnostic-load, resource-warning and critical-safety events.
- Stabilized diagnostic CPU load generation below the 50% hard cap.

## Upgrade

The installer supports upgrading an existing DeskPulse installation. The activity database and exports under `Documents\DeskPulse` are preserved.

## Installer

Release asset:

```text
DeskPulse_Setup_0.3.1.0.exe
```

## Verification

The local 0.3.1.0 build, upgrade and acceptance checklist passed on 2026-07-22.
