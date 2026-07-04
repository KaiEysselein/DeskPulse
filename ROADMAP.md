# DeskPulse Roadmap

This roadmap tracks planned work and ideas for future DeskPulse versions.

The roadmap is not a changelog. Completed changes belong in `CHANGELOG.md`. Detailed bugs, screenshots, test notes, and discussion should preferably be tracked as GitHub Issues.

---

## Current baseline

```text
0.1.0
```

Version 0.1.0 is the first more complete early baseline. It includes:

- selected file activity logging
- user/session activity logging
- program start/close logging
- configurable Excel export worksheets and fields
- date-range export
- export progress percentage/status text
- general settings including optional start with Windows logon
- split tray menus
- quieter normal operation without notification balloons

---

## Suggested next version: 0.1.1 — Data Management and Logging Filters

Planned / candidate work:

- Add a database size display in settings.
- Add optional database size warning or limit.
- Add a manual clear database action.
- Add a safer clear/export cleanup action for generated files only.
- Add confirmation dialogs for destructive cleanup actions.
- Add excluded paths/folders list.
- Add excluded processes/programs list.
- Add default exclusions for noisy Windows/system paths.
- Add default exclusions for temporary/background files created when programs start.
- Add options to exclude activity from common system processes where useful.
- Improve explanations in settings so users understand what is and is not logged.

Notes:

- Database clearing should preserve the database file structure but remove selected records.
- Consider separate clearing options for file activity, user/session activity, and program activity.
- Filtering should avoid hiding important user-created files by default.

---

## Suggested later version: 0.1.2 — Export Improvements

Planned / candidate work:

- Add export cancellation.
- Add export preview/summary before opening Excel.
- Improve export performance on large date ranges.
- Add warning if selected date range contains a very large number of rows.
- Add export file naming options.
- Add optional export to a user-selected folder.
- Add last-used export date range setting.

---

## Suggested later version: 0.1.3 — Startup and Deployment Polish

Planned / candidate work:

- Improve Task Scheduler startup handling and diagnostics.
- Add a status indicator showing whether Windows startup is enabled and valid.
- Detect if the saved startup path no longer exists.
- Add clearer guidance for portable-folder deployment.
- Stabilize publish-folder packaging.
- Investigate installer packaging.

---

## Suggested later version: 0.2.0 — Program Activity Enhancements

Planned / candidate work:

- Improve program start/close detection accuracy.
- Add more useful program metadata where available.
- Add program grouping by executable name.
- Add duration tracking for program sessions.
- Add summary worksheet for program usage.
- Add configurable include/exclude list for program logging.

---

## Email logging idea — not currently planned for 0.1.x

Email logging is technically possible only in limited scenarios and is more privacy-sensitive than file/program logging.

Candidate approach for a future version, if ever added:

- Outlook sent-email metadata only.
- Disabled by default.
- Current user only.
- No email body storage.
- No hidden/stealth monitoring.
- Clear warning in settings.

Email read/open logging is not recommended for the current DeskPulse roadmap.

---

## Bug tracking recommendation

Use GitHub Issues for live bugs and detailed future tasks.

Suggested labels:

```text
bug
enhancement
maintenance
privacy
export
database
logging-filter
program-activity
ui
v0.1.1
v0.1.2
```

Suggested issue examples:

```text
Feature: Add database size display and limit
Feature: Add manual clear database option
Feature: Add excluded paths and processes
Feature: Filter noisy Windows/system files
Feature: Add export cancellation
Bug: Export progress incorrect for empty worksheet selection
Bug: Startup task path becomes invalid after moving portable folder
```
