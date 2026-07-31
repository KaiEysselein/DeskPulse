# Version Check - 0.4.0.6

Version **0.4.0.6** is the current patch release. Version **0.4.0.0** remains the retained milestone baseline.

## Active references

- `DeskPulse.Shared` AppInfo: `0.4.0.6`
- Shared, service and tray project versions: `0.4.0.6`
- Publish folders: `dev\publish\v0.4.0.6\service` and `dev\publish\v0.4.0.6\tray`
- Installer: `dev\publish\v0.4.0.6\installer\DeskPulse_Setup_0.4.0.6.exe`
- Current approved installer: `releases\current\DeskPulse_Setup_0.4.0.6.exe`
- Retained milestone: `releases\v0.4.0.0`
- GitHub tag target: `v0.4.0.6`
- Release code commit: `6fd84ca`
- Installer size: `83,207,602 bytes`
- Installer SHA-256: `413A8E7B4C3C8002BF8D582C4C93F4BB090832CD99A87DA32FAA8D681588E1AE`

## Automated verification

- [x] Release build completed successfully.
- [x] All **81** automated tests passed.
- [x] No automated tests failed or were skipped.
- [x] Self-contained service and tray outputs were created under `dev\publish\v0.4.0.6`.
- [x] Inno Setup created `DeskPulse_Setup_0.4.0.6.exe`.
- [x] The approved installer was copied to `releases\current`.
- [x] Calendar query coverage includes selected and unselected File, App and User Activity records.
- [x] Regression coverage verifies independent Calendar tab grouping state.

## Installed acceptance

- [x] Upgrade installation completed successfully.
- [x] Installed service and tray reported file version `0.4.0.6`.
- [x] `DeskPulse.Service` was Running with Automatic startup.
- [x] Exactly one service process and one tray process were active.
- [x] Current-user Log opened and user-scoped record deletion succeeded.
- [x] Calendar displayed separate Files, Apps and User Activity tabs.
- [x] Calendar grouping and expanded/collapsed state remained independent across tabs.
- [x] Repeated tab switching no longer produced the reported erratic collapse-state behaviour.
- [x] The installer no longer displayed an Additional Tasks page.
- [x] Fresh-install defaults are desktop shortcut enabled and startup message enabled.
- [x] Upgrade and same-version reinstall logic preserves existing desktop-shortcut and startup-message choices.
- [x] The final rebuilt installer was accepted through manual installation and runtime use.

## Release boundary

DeskPulse provides no combined all-users log. Current-user actions target only the calling user's SID database; administrator log and maintenance target only the protected System database.

Generated `bin`, `obj` and `publish` folders are not retained as source history. `releases\v0.4.0.0` remains the retained 0.4 milestone installer.

