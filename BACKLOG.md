# DeskPulse Backlog

## Open enhancements

### Medium Feature — Service-owned system and per-user database layout

- Move live activity databases out of the user's Documents folder and into service-owned storage under `C:\ProgramData\DeskPulse`.
- Store machine-wide service, installation, safeguard and unattributable events in `C:\ProgramData\DeskPulse\System\DeskPulse-System.db`.
- Store user-attributable activity in `C:\ProgramData\DeskPulse\Users\<Windows-SID>\DeskPulse.db`.
- Record event scope, Windows SID and session ID so the service can route each event to the correct database without discarding system-wide activity or assigning it to the wrong user.
- Keep each database and its SQLite WAL/SHM files together on a local, non-synchronized volume.
- Resolve the interactive user's Windows SID explicitly instead of deriving paths from the LocalSystem service profile.
- Create the system and SID-specific directories with explicit access limited to `SYSTEM`, administrators and authorized users.
- Preserve the service as the sole SQLite writer and grant the tray only the access required for read-only views, statistics and exports.
- Allow ordinary users to view, print and export system-database records, but never modify or remove those records.
- Provide an explicitly authorized administrative machine-wide view for combining system records with permitted per-user records without weakening database permissions.
- Separate machine-wide service settings from per-user settings and activity data.
- Keep system-wide logging rules service-owned and editable only through an explicitly authorized administrative interface.
- Allow each user to create, edit, enable and disable only the rules associated with that user's SID.
- Prevent per-user rules from suppressing, modifying, rerouting or deleting system-wide events and records.
- Show system-wide rules to ordinary users as read-only when they need visibility into what DeskPulse records.
- Keep the normal tray process unelevated and provide an **Administrator settings...** action that opens a separate, short-lived process through the standard Windows UAC `runas` flow.
- Expose system rules, system database configuration, retention, cleanup, service safeguards and authorized machine-wide views only in the elevated administrator window.
- End administrator access when the elevated window closes and require fresh UAC approval whenever it is opened again.
- Require the service to authorize every administrative named-pipe request by verifying the client's elevated token, local Administrators membership and expected executable identity; do not rely on hidden or disabled UI controls for security.
- Safely migrate the existing database and settings from the configured Documents-based location, with backup, validation and rollback on failure.
- Handle users whose profile is unavailable, removed or renamed without redirecting data into another user's folder.
- Verify install, upgrade, uninstall, database cleanup, export and multi-session behavior with the new layout.
- Complete this storage foundation before enabling multi-user architecture or all-user tray startup.

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
