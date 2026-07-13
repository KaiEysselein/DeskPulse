# DeskPulse Roadmap

## Current verified baseline

```text
0.1.3.1
```

This is the source baseline to build, publish, tag, and synchronise with GitHub before further changes.

Current capabilities include:

- ETW file activity logging controlled by ordered rules
- app start/stop logging controlled by ordered rules
- user/session activity logging controlled by ordered rules
- JSON registry schema 4 for File, Folder, User, and App rule lists
- JSON import/export for File, Folder, and App rules
- View Log with four activity views, record IDs, 500-row paging, and current-page export
- full-record details and rule creation from selected log rows
- optional historical cleanup and database compaction
- normal Settings Maintenance tab for full-history rule housekeeping
- left-click-only tray menu
- no debug logging and no Maintenance command-line mode

## Next version: 0.1.4.0

Candidate priorities after the `0.1.3.1` baseline is released:

- performance profiling of Settings, View Log, and installed-app loading
- asynchronous loading with visible progress where useful
- review whether Folder Activity should remain a separate rules/view concept
- improve database query/index performance for large histories
- add cancellation to long-running export and cleanup operations
- add backup/restore safeguards before destructive housekeeping
- improve current-page export naming and destination selection
- add automated migration and rule-engine tests
- add installer/deployment packaging after portable publishing is stable

## Later ideas

- app session duration summaries
- richer program metadata
- optional user-selected log retention policy
- database backup scheduling
- installer with upgrade/uninstall support

Completed work belongs in `CHANGELOG.md`; detailed defects and test evidence should be tracked as GitHub Issues.
