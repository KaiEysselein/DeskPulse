# DeskPulse Roadmap

## Current release: 0.3.1.0

- Database-cleanup window-handling correction
- Installation lifecycle activity logging
- Transparent tray-state icons
- Existing service resource safeguards and diagnostic tests

## Planned

### Machine-wide tray startup

Consider optional installer support for starting DeskPulse Tray for every Windows user who logs on. Before implementation, resolve:

- concurrent user-session behaviour;
- one tray instance per user session;
- shared versus per-user settings;
- activity database path and ownership;
- interaction with the LocalSystem service.

Prefer an **At logon of any user** scheduled task over merely changing the current HKCU Run registration to HKLM.

### Pause-state model

Retain the planned distinction between:

- **Pause for this session**, which resets after restart; and
- **Pause indefinitely**, which persists until the user explicitly resumes logging.

Persistent pause should remain the safety behaviour following a critical service-resource trigger where configured.
