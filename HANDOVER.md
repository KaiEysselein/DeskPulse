# DeskPulse Repository and Release Handover

## Release status

- Current released version: **0.4.0.5**
- Maintenance release target: **0.4.0.6**
- Repository: `https://github.com/KaiEysselein/DeskPulse`
- Current release tag: `v0.4.0.5`
- Retained milestone folder: `releases\v0.4.0.0`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.5.exe`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.4.0.5 is the released baseline. It provides collapsible grouped rows and summaries across File, App and User Activity report tables.

## Unreleased working tree

The working tree contains a Calendar refinement intended for the 0.4.0.6 maintenance release:

- separate Files, Apps and User Activity Calendar tabs;
- each tab displays and exports only its own activity type;
- Calendar loads only records explicitly selected for Calendar view;
- the former All records/Marked records toggle and registry preference are removed;
- query coverage includes selected and unselected records across all three activity sources.

This work has not yet been accepted as a release. Build, full automated tests, installer creation and runtime verification remain required.

## Architecture and security boundary

- The Windows service is the sole SQLite writer.
- System and per-user databases are isolated under protected ProgramData folders.
- Current-user views target only the calling user's SID database.
- Elevated administrator views target only the protected System database.
- DeskPulse provides no combined all-users log or administrator maintenance path into another user's database.
- State-changing named-pipe commands are authorized by the service.

## Release verification

`VERSION_CHECK.md` records the accepted release verification. It currently applies to 0.4.0.5 and must not be rewritten for 0.4.0.6 until the new build and runtime checks have passed.

## Release retention

- `releases\v0.4.0.0` remains the retained 0.4 milestone installer.
- Patch installers replace the contents of `releases\current` after acceptance.
- Numbered patch installers are attached to their corresponding GitHub releases.
- Generated `dev\publish`, `bin` and `obj` content is disposable and should not be retained as source history.

## Next work

1. Complete and verify the Calendar refinement.
2. Run the 0.4.0.6 build, automated tests, publish and installer workflow.
3. Perform installed upgrade and runtime acceptance checks.
4. Update README, CHANGELOG, VERSION_CHECK and both handovers only after acceptance.
5. Commit and publish release `v0.4.0.6`.
6. Continue distinct session-only and persistent pause modes.
7. Add further concurrent-session runtime regression coverage.
