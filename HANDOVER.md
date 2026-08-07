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


## Local Working Folders

DeskPulse uses two local-only folders inside the repository for housekeeping and working files:

- `Backups\` - for local DeskPulse backups, Git bundles, preserved release installers, recovery copies, and other files that should not be synced to GitHub.
- `Temp\` - for temporary files, staging files, generated working files, exports, intermediate packages, handover packages, and other disposable development artifacts.

Both folders are excluded from Git through `.gitignore`.

### Standing working rule

- Use `D:\Kai\GitHub\DeskPulse\Temp` for all temporary or generated working material, including files downloaded or created by PowerShell, shell commands, scripts, build helpers, or AI-assisted development work. Do not use the Windows user's Downloads folder for DeskPulse project work.
- Use `D:\Kai\GitHub\DeskPulse\Backups` for local backups and recovery snapshots.
- Do not place temporary, generated, backup, or handover files elsewhere in the repository when they can be stored in one of these two folders.
- Build output such as `bin`, `obj`, `publish`, test artifacts, verification logs, and locally preserved release binaries must remain untracked unless there is a specific documented reason to commit them.
- Keep the tracked Git working tree clean and limited to source code, tests, documentation, installer definitions, scripts, workflows, and intentionally version-controlled assets.


## AI-Assisted Development Workflow

Root-level `AGENTS.md` defines the standing workflow for Codex and other AI-assisted DeskPulse development.

- Keep requested work narrowly scoped and inspect only the files needed for the task.
- Do not perform unrelated refactoring or repository-wide audits unless specifically requested.
- Use `D:\Kai\GitHub\DeskPulse\Temp` for AI-assisted shell downloads, temporary scripts, generated files, extracted files, intermediate artifacts, and other disposable working material.
- Use `D:\Kai\GitHub\DeskPulse\Backups` for persistent local backups and recovery copies.
- `Temp/` and `Backups/` are local-only and must never be committed or synced to GitHub.
- Do not use the Windows user's general Downloads folder for DeskPulse project working files.
- Before committing or pushing, verify Git status, relevant ignored files, and the relevant diff.
- If local and remote history diverge or the remote appears to have been force-updated, inspect the history before taking corrective action; do not force-push blindly.
- Keep changes in small logical commits and run only checks or tests relevant to the task.
