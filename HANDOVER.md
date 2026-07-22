# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse release candidate is **0.3.2.0**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.2.0`
- Retained release folder: `releases\v0.3.2.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.2.0 adds a focused administrator-settings process boundary: ordinary settings contains General and Rules, while Maintenance opens separately through Windows UAC and exits with its window. Service-side authorization and the ProgramData database redesign remain backlog work.

## Repository structure

```text
D:\Kai\GitHub\DeskPulse\
├── .git\
├── README.md
├── CHANGELOG.md
├── GITHUB_RELEASE.md
├── ROADMAP.md
├── BACKLOG.md
├── HANDOVER.md
├── LICENSE
├── dev\
│   ├── HANDOVER.md
│   ├── DeskPulse.sln
│   ├── Installer\
│   ├── Resources\
│   ├── scripts\
│   ├── src\
│   └── docs\
└── releases\
    ├── current\
    ├── v0.3.0.0\
    └── v0.3.2.0\
```

The repository-root handover records release and public-project continuity. Detailed architecture, implementation, build, safeguards and technical verification belong in `dev\HANDOVER.md`.

## Release-retention policy

Release versions whose fourth component is zero, matching `v0.x.x.0`, are retained permanently under `releases\v<version>` and may receive a formal GitHub Release. Intermediate working builds retain their exact internal version but replace the approved artifacts under `releases\current`.

Historical entries must remain unchanged in archived verification records. Active version references must reflect the current working or released version.

## 0.3.2.0 scope

- Fix database-cleanup confirmation and Settings window lifetime.
- Log DeskPulse installation, update and reinstallation under User Activity.
- Correct transparency in all Normal, Paused and Warning tray-state assets.
- Log diagnostic-load, resource-warning and critical-safety-pause events under User Activity.
- Stagger diagnostic CPU workers and reserve duty-cycle headroom below the 50% hard cap.
- Preserve service-only SQLite write ownership.
- Retain all service CPU/RAM safeguards and hard diagnostic caps.
- Keep ordinary Settings unelevated with General and Rules only.
- Launch a separate, short-lived Administrator settings process through Windows UAC for Maintenance only.
- Validate the administrator command-line mode is actually elevated.
- Update README, CHANGELOG, release notes, roadmap, backlog and both handovers.

## Verification status

Release 0.3.2.0 passed build, publish and installer compilation on 2026-07-22. Installation, administrator-settings UAC behavior and the corrected View Log export flow were interactively confirmed.

## Future work

- **0.3.2.x — Service-owned system and per-user database layout:** move storage to ProgramData, route records by scope and SID, migrate safely, separate rule ownership and enforce service-side named-pipe authorization. Version 0.3.2.0 did not implement this architecture.

- **Medium Feature — Multi-user architecture and all-user tray startup:** resolve per-user settings and databases, session-aware tray instances, named-pipe identity and safe migration before enabling machine-wide startup.
- **Medium Feature — Calendar activity view:** add a View Log calendar with monthly daily summaries, hourly drill-down, selectable activity categories and efficient SQL aggregation.
- **Medium Feature — Pause-state model:** distinguish session-only and persistent pause modes with clear tray states.

## Release procedure

Build and verify from `dev`. The approved installer must be present under both `releases\current` and `releases\v0.3.2.0`. The formal GitHub Release uses tag `v0.3.2.0`.
