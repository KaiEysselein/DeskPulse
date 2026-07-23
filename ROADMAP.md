# DeskPulse Roadmap

## Current release: 0.3.3.0

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

## Planned

### Calendar activity view

Add a Calendar view under Log with month, day and hourly drill-down.

- Month cells show compact selectable daily summaries.
- Double-clicking a day opens hourly summaries.
- Double-clicking an hour opens the existing log filtered to that hour.
- Aggregate efficiently in SQLite rather than loading raw rows.

### Pause-state model

Distinguish:

- **Pause for this session**, which resets after restart; and
- **Pause indefinitely**, which persists until explicitly resumed.

Persistent pause remains available for critical service-resource safeguards.

### Runtime regression coverage

- Repeat two-user simultaneous-session acceptance after future routing changes.
- Verify scheduled-task startup after clean installation and Windows sign-in.
- Add automated coverage for single-window tray behavior where practical.
