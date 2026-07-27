# DeskPulse Repository and Release Handover

## Current release

The current DeskPulse patch release is **0.3.3.2**.

- Repository: `https://github.com/KaiEysselein/DeskPulse`
- GitHub release tag: `v0.3.3.2`
- Retained milestone folder: `releases\v0.3.3.0`
- Current approved installer copy: `releases\current`
- Active development source: `dev`
- Detailed technical handover: `dev\HANDOVER.md`

Version 0.3.3.2 locks in the tested tray-menu dispatch, Windows 11 hidden-icons interaction, Excel-export progress/closure, and runtime tray-state icon fixes before Calendar development.

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

The final 0.3.3.2 build, publish, installer and installed-version results are recorded in `VERSION_CHECK.md`.

## Release-retention policy

Release versions whose fourth component is zero are retained under `releases\v<version>`. Patch releases replace `releases\current` and are attached to their GitHub Release without replacing the retained milestone folder.

## Future work

- Calendar activity view with month, day and hourly SQL aggregation.
- Clear distinction between session-only and persistent pause modes.
- Additional runtime regression coverage for concurrent Windows sessions.

## Release procedure

Build and verify from `dev`. The approved patch installer must be present under `releases\current`; `releases\v0.3.3.0` remains the retained milestone baseline. The GitHub Release uses tag `v0.3.3.2`.
