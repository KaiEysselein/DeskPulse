# DeskPulse Repository and Release Handover

## Current retained release

The current accepted and permanently retained DeskPulse milestone is **0.3.0.0**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.0.0`
- Retained release folder: `releases\v0.3.0.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.0.0 promotes the tested 0.2.2.3 service-safeguard baseline. The promotion itself introduced no additional runtime feature.

## Repository structure

```text
D:\Kai\GitHub\DeskPulse\
├── .git\
├── HANDOVER.md
├── GitHub-facing documentation
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
    └── v0.3.0.0\
```

The repository-root handover records release and repository continuity. Detailed implementation, build, architecture, safeguards, diagnostic commands and technical verification belong in `dev\HANDOVER.md`.

## Release-retention policy

Only milestone versions matching `v0.x.0.0` are retained permanently under `releases\v<version>` and may receive a formal GitHub Release. Intermediate builds retain their exact internal version but replace the approved artifacts under `releases\current`.

Historical version entries must remain unchanged in `CHANGELOG.md` and archived verification records. Active version references must reflect the current working or released version.

## 0.3.0.0 release scope

The milestone includes service CPU and RAM safeguards, sustained warning and critical thresholds, critical safety pause, restart-persistent pause enabled by default, explicit recovery through **Resume Logging**, safeguard settings under **Settings → Maintenance**, and controlled diagnostic service-load tests hard-capped at 50% CPU and 50% RAM.

## Known deferred item

The tray-state icon artwork may show a non-transparent background on some Windows themes. This is a deferred visual correction and is not represented as completed in 0.3.0.0.

## Future work

- Correct transparency in all tray-state icon assets.
- Consider optional installer support for starting the tray for all Windows users. Resolve concurrent sessions, per-session duplicate prevention, shared versus per-user settings, and database ownership before implementation.
- Keep future repository-facing and technical handovers separate and update each only for its intended scope.

## Release procedure

Build and verify from `dev`. For the accepted 0.3.0.0 milestone, the installer should be present under both `releases\current` and `releases\v0.3.0.0`. The corresponding formal GitHub Release uses tag `v0.3.0.0`.
