# DeskPulse 0.2.0.1 Database Write Audit

The tray application has been audited for SQLite write access.

## Service-owned write operations

The following operations are now sent over the named pipe and executed by `DeskPulse.Service`:

- Delete selected File Activity records
- Delete selected App Activity records
- Delete selected User Activity records
- Create-rule cleanup of historical records
- Database Housekeeping using current rules
- Remove records matching current exclusions
- Clear all File Activity records
- Clear all App Activity records
- Clear all User Activity records
- Clear all activity tables
- SQLite schema initialization and migration
- SQLite `VACUUM`
- Live File, App, and User Activity inserts

## Tray database access

The tray now opens SQLite in read-only mode for:

- View Log
- Date-range discovery
- Conflict counts before cleanup
- Maintenance database overview
- Maintenance Top 100 statistics
- Export/report reads

No `SqliteOpenMode.ReadWrite`, `SqliteOpenMode.ReadWriteCreate`, SQL `DELETE`, `INSERT`, `UPDATE`, `VACUUM`, or direct database transaction remains in the tray project.

## Selected-record deletion stability

- Fixed selected-record deletion waiting indefinitely: service write commands now use the monitor-owned database instance and shared database lock instead of pausing ETW and opening a competing database instance.
- View Log deletion now awaits the service asynchronously so the form remains responsive.
