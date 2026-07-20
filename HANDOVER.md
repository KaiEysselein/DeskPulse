# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse release candidate is **0.3.1.0**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.1.0`
- Retained release folder: `releases\v0.3.1.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.1.0 promotes the completed 0.3.0.1 correction work: database-cleanup window handling, installation lifecycle activity logging and transparent tray-state assets.

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
- Preserve service-only SQLite write ownership.
- Retain all service CPU/RAM safeguards and hard diagnostic caps.
- Update README, CHANGELOG, release notes, roadmap, backlog and both handovers.

## Verification status

The source and documentation are prepared for 0.3.1.0. Final release approval requires successful local build, installer generation, upgrade testing and completion of the acceptance checklist in `dev\HANDOVER.md`.

## Future work

- Optional machine-wide tray startup for all Windows users.
- Resolve concurrent sessions, per-session duplicate prevention, shared versus per-user settings and database ownership before implementing machine-wide startup.

## Release procedure

Build and verify from `dev`. The approved installer must be present under both `releases\current` and `releases\v0.3.1.0`. The formal GitHub Release uses tag `v0.3.1.0`.
