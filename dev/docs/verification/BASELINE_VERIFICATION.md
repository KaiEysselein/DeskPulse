# DeskPulse 0.3.0.0 Baseline Verification

This package is the authoritative DeskPulse 0.3.0.0 milestone source baseline, subject to successful local compilation and final installation verification.

Verified in the source package:

- Active application version constant: 0.3.0.0
- Shared, Service and Tray project versions: 0.3.0.0
- Installer version and output filename: 0.3.0.0
- Publish folders: `dev\publish\v0.3.0.0\service` and `dev\publish\v0.3.0.0\tray`
- Milestone retention folder: `releases\v0.3.0.0`
- Current approved installer folder: `releases\current`
- Service safeguard monitor, critical pause and recovery are present.
- Safeguard settings are exposed under Settings → Maintenance.
- Critical-pause restart persistence defaults to enabled.
- Diagnostic CPU and RAM load generation is capped service-side at 50% per resource.
- Tray source retains read-only database access; service owns database writes.
- Historical version references remain in archived documents only.

Final acceptance requires successful execution of the build/publish/installer chain and the 0.3.0.0 acceptance checklist in `HANDOVER.md`.
