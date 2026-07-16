# DeskPulse Feature Backlog

This file records feature and enhancement requests that are intentionally excluded from the 0.2.2.2 cleanup release.

## Pause modes and icon states

### Pause for this session

- Non-persistent pause.
- Resets after the DeskPulse service or Windows restarts.
- Uses a distinct tray icon so the temporary state is immediately visible.

### Pause indefinitely

- Persistent pause across service and Windows restarts.
- Remains active until the user explicitly enables logging.
- Uses a separate tray icon from the session-only pause state.

### Critical service-threshold response

- Integrate the persistent pause state with the planned DeskPulse service CPU/RAM safeguards.
- At a sustained warning threshold, log one warning and continue.
- At a sustained critical threshold, write a critical diagnostic record and fallback marker, then stop or disable logging safely.
- Keep the tray running so it can explain the condition and offer the controlled recovery action.
- Do not automatically resume logging after restart; require explicit user re-enablement after the cause has been addressed.
