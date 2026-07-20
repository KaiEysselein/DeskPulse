# DeskPulse Backlog

## Open enhancements

### Medium Feature — Multi-user architecture and all-user tray startup

- Introduce per-user settings and activity storage keyed by Windows SID.
- Support one tray instance per interactive Windows session.
- Verify named-pipe client identity and separate user-specific commands from machine-administration commands.
- Migrate the existing single-user settings and database safely.
- Add optional machine-wide tray startup only after user isolation is complete.

### Medium Feature — Calendar activity view with drill-down

Add a new **Calendar** view under **View Log**.

- Month view showing compact daily summaries.
- Double-click a day to open an hourly summary for that date.
- Double-click an hour to open the normal log view filtered to that hour.
- Allow the user to choose which activity appears in the calendar, including file activity, file type, application activity, user activity and Explorer activity.
- Support summary options such as total file activity, files created/modified/deleted, leading file types, application launches, application-use duration, active time, first activity and last activity.
- Limit crowded summaries and group excess categories under **Other**.
- Use grouped SQL queries by date and hour rather than loading all matching records into memory.
- Allow the selected date or hour to feed into filtered logs, statistics and export.

### Medium Feature — Pause-state model

- Implement distinct session-only and persistent pause modes with clear tray icon states.
- Keep persistent pause as the configured safety behaviour following a critical service-resource trigger.

### Ongoing verification

- Continue runtime testing of all form buttons and tray-menu actions as new defects are reported.

## Closed in 0.3.1.0

- Settings disappeared when **Clean database with current rules...** was clicked.
- Tray-state PNG/ICO assets displayed non-transparent backgrounds.
- Installation, update and reinstallation were not recorded under User Activity.
