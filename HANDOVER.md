# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse release candidate is **0.3.1.0**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.1.0`
- Retained release folder: `releases\v0.3.1.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.1.0 promotes the completed 0.3.0.1 correction work: database-cleanup window handling, installation lifecycle activity logging, transparent tray-state assets, reliable safeguard-event logging and stabilized diagnostic CPU load generation.

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
    └── v0.3.1.0\
```

The repository-root handover records release and public-project continuity. Detailed architecture, implementation, build, safeguards and technical verification belong in `dev\HANDOVER.md`.

## Release-retention policy

Release versions whose fourth component is zero, matching `v0.x.x.0`, are retained permanently under `releases\v<version>` and may receive a formal GitHub Release. Intermediate working builds retain their exact internal version but replace the approved artifacts under `releases\current`.

Historical entries must remain unchanged in archived verification records. Active version references must reflect the current working or released version.

## 0.3.1.0 scope

- Fix database-cleanup confirmation and Settings window lifetime.
- Log DeskPulse installation, update and reinstallation under User Activity.
- Correct transparency in all Normal, Paused and Warning tray-state assets.
- Log diagnostic-load, resource-warning and critical-safety-pause events under User Activity.
- Stagger diagnostic CPU workers and reserve duty-cycle headroom below the 50% hard cap.
- Preserve service-only SQLite write ownership.
- Retain all service CPU/RAM safeguards and hard diagnostic caps.
- Update README, CHANGELOG, release notes, roadmap, backlog and both handovers.

## Verification status

Release 0.3.1.0 passed local build, publish, installer, upgrade and acceptance verification on 2026-07-22. The approved installer is retained under both release locations.

## Future work

- **Medium Feature — Multi-user architecture and all-user tray startup:** resolve per-user settings and databases, session-aware tray instances, named-pipe identity and safe migration before enabling machine-wide startup.
- **Medium Feature — Calendar activity view:** add a View Log calendar with monthly daily summaries, hourly drill-down, selectable activity categories and efficient SQL aggregation.
- **Medium Feature — Pause-state model:** distinguish session-only and persistent pause modes with clear tray states.

## Release procedure

Build and verify from `dev`. The approved installer must be present under both `releases\current` and `releases\v0.3.1.0`. The formal GitHub Release uses tag `v0.3.1.0`.
