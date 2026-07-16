# DeskPulse Roadmap

## Current baseline: 0.3.0.0

Version `0.3.0.0` is the accepted milestone release containing the tested service CPU/RAM safeguards, controlled diagnostic load testing, sustained warning and critical thresholds, persistent critical safety pause, and user-configurable safeguard settings under **Settings → Maintenance**.

## Completed in the 0.3.0.0 milestone

- Monitor `DeskPulse.Service` CPU and RAM use.
- Configurable warning and critical thresholds and sustained durations.
- Warning-state tray indication while logging continues.
- Critical diagnostic event and controlled logging pause.
- Optional persistent pause after a critical trigger, enabled by default.
- Explicit **Resume Logging** recovery.
- CPU, memory, and combined diagnostic load tests with a hard service-side 50% cap.
- Live load-test window with progress, measured values, and **Stop Test**.

## Repository organization

- Keep GitHub-facing documentation and Git metadata at the repository root.
- Keep application source, build scripts, installer definitions, shared resources, and technical verification records under `dev`.
- Keep approved local installer and handover artifacts under `releases`.
- Treat `dev\publish` as temporary generated output.

## Release retention

- Retain permanent installer archives and GitHub Releases only for milestone versions matching `v0.x.0.0`.
- Replace `releases\current` for each approved intermediate build.
- Preserve exact internal version numbers in diagnostics, installers, handovers, and source history.

## Next priorities

- Correct tray-state icon transparency.
- Review and implement optional all-user tray startup with sound multi-session behaviour.
- Complete clean-PC installation, upgrade, restart, safeguard-recovery, and uninstall regression testing.
- Add structured/versioned named-pipe contracts and richer service diagnostics.
- Add installer logging and clearer upgrade/recovery reporting.
- Add automated database backup and restore controls.

## Later

- Code-sign release binaries and installer.
- Add an automated GitHub build/release workflow.
- Add optional retention policies and performance controls for very large databases.
