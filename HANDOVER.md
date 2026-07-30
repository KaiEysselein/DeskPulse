# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse patch release is **0.4.0.5**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.4.0.5`
- Retained milestone folder: `releases\v0.4.0.0`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.5.exe`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.4.0.5 provides collapsible grouped rows and summaries across File, App and User Activity report tables.

## Architecture and security boundary

- The Windows service remains the sole SQLite writer.
- System and per-user databases remain isolated under protected ProgramData folders.
- Current-user views target only the calling user's SID database.
- Elevated administrator views target only the protected System database.
- DeskPulse provides no combined all-users log or administrator maintenance path into another user's database.
- State-changing commands remain authorized by the service.

## Verification

The Release build, 79 automated tests, installer build, silent upgrade, installed versions, service state, process count, SQLite integrity and binary hashes are recorded in `VERSION_CHECK.md`.

## Release retention

Version `0.4.0.0` remains the retained milestone folder. Patch installers replace `releases\current` and are attached to their numbered GitHub releases.

## Next work

- Distinct session-only and persistent pause modes.
- Runtime regression coverage for concurrent Windows sessions.
- Further Calendar aggregation and drill-down enhancements if required.
