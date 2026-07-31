# DeskPulse Repository and Release Handover

## Current release

- Current released version: **0.4.0.6**
- Repository: `https://github.com/KaiEysselein/DeskPulse`
- Release tag: `v0.4.0.6`
- Release code commit: `6fd84ca`
- Retained milestone folder: `releases\v0.4.0.0`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.6.exe`
- Installer SHA-256: `413A8E7B4C3C8002BF8D582C4C93F4BB090832CD99A87DA32FAA8D681588E1AE`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.4.0.6 separates Calendar records into Files, Apps and User Activity tabs, restricts each tab and export to its own activity type, keeps grouping state independent per tab, and preserves installer choices during upgrades.

## Architecture and security boundary

- The Windows service is the sole SQLite writer.
- System and per-user databases are isolated under protected ProgramData folders.
- Current-user views target only the calling user's SID database.
- Elevated administrator views target only the protected System database.
- DeskPulse provides no combined all-users log or administrator maintenance path into another user's database.
- State-changing named-pipe commands are authorized by the service.

## Release verification

`VERSION_CHECK.md` records the 0.4.0.6 build, 81-test suite, publish, installer and installed acceptance results.

## Release retention

- `releases\v0.4.0.0` remains the retained 0.4 milestone installer.
- Patch installers replace `releases\current` after acceptance.
- Numbered installers are attached to their corresponding GitHub releases.
- Generated `dev\publish`, `bin` and `obj` content is disposable and should not be retained as source history.
- Historical verification or rollback evidence should be archived outside the active repository when no longer needed day to day.

## Next work

1. Distinct session-only and persistent pause modes.
2. Further concurrent-session runtime regression coverage.
3. Calendar daily summaries and optional hour-level drill-down.
4. Additional grouped SQLite aggregation where it materially improves large-view performance.

