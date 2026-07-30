# DeskPulse Roadmap

## Current release: 0.4.0.4

- Protected system and per-user ProgramData databases and settings
- SID, scope and Windows-session attribution
- Simultaneous-session database and rule routing
- Safe legacy database migration with backup and rollback
- Service-side named-pipe client authorization
- Isolated current-user and administrator system interfaces
- All-users scheduled tray startup with one tray per session
- Complete date-range export with progress
- Optional folder-opening suppression
- One DeskPulse form open from the tray at a time
- Integrated Records and Calendar layouts
- Calendar marking, All/Marked filtering and expandable grouping across File, App and User Activity
- Calendar Details-column grouping and summarisation
- Per-user startup status and time-format preferences
- Animated progress feedback for large view operations

## Planned

### Calendar aggregation enhancements

- Add compact daily summary metrics to month cells.
- Add optional hour-level drill-down summaries.
- Move further high-volume aggregation into grouped SQLite queries.

### Pause-state model

Distinguish:

- **Pause for this session**, which resets after restart; and
- **Pause indefinitely**, which persists until explicitly resumed.

Persistent pause remains available for critical service-resource safeguards.

### Runtime regression coverage

- Repeat two-user simultaneous-session acceptance after future routing changes.
- Verify scheduled-task startup after clean installation and Windows sign-in.
- Add automated coverage for single-window tray behavior where practical.
