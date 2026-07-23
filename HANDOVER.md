# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse release candidate is **0.3.3.0**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.3.0`
- Retained release folder: `releases\v0.3.3.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.3.0 promotes the completed 0.3.2.x storage, attribution, authorization, multi-session and user/system UI work.

## Release scope

- Store system activity and settings under the protected ProgramData System folder.
- Store each user's activity and settings under a protected Windows-SID folder.
- Attribute new activity by scope, SID and Windows session.
- Route simultaneous interactive sessions to their own database writers and rule sets.
- Migrate the legacy Documents database safely with backup, integrity validation and rollback.
- Authorize state-changing named-pipe commands from the installed tray and require elevation for system operations.
- Provide isolated **Current User** Log, Settings and Maintenance and UAC-elevated System Log, Settings and Maintenance.
- Provide no combined all-users log and no administrator maintenance path into another user's database.
- Export the complete active tab and date range with progress reporting.
- Optionally suppress folder-opening events while preserving extensionless-file logging.
- Allow only one DeskPulse form to be open from the tray at a time.
- Launch one unelevated tray per Windows session through the all-users scheduled task.

## Verification status

The underlying 0.3.2.x migration, ACL, schema, routing, simultaneous-session, scheduled-task, named-pipe authorization, split-settings, isolated-UI, maintenance and export slices were interactively verified on 2026-07-23. The exact evidence is recorded in `dev\docs\verification\STORAGE_ACCEPTANCE_0.3.2.x.md`.

The final 0.3.3.0 build, publish and installer results are recorded in `VERSION_CHECK.md`. The newest single-window tray guard remains pending a short runtime confirmation.

## Release-retention policy

Release versions whose fourth component is zero are retained under `releases\v<version>` and may receive a formal GitHub Release. Historical changelog entries and archived verification records remain unchanged.

## Future work

- Calendar activity view with month, day and hourly SQL aggregation.
- Clear distinction between session-only and persistent pause modes.
- Additional runtime regression coverage for concurrent Windows sessions.

## Release procedure

Build and verify from `dev`. The approved installer must be present under both `releases\current` and `releases\v0.3.3.0`. The formal GitHub Release uses tag `v0.3.3.0`.
