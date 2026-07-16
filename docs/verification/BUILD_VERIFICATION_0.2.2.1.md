# DeskPulse 0.2.2.1 Build Verification


Tray-opened forms close automatically after external focus loss, and log views support a persisted 24-hour or 12-hour AM/PM time display.
## Scope

- Promoted the complete audited 0.2.1.8 feature set to the GitHub release baseline 0.2.2.1.
- File Activity grid includes the visible **Activity** column.
- Activity uses `InferredAction` when present and falls back to `ActivityType`.
- File Activity grouping includes **Activity**.
- The Settings tab no longer exposes **Export Options**.
- Existing/default export configuration is preserved internally for standard exports.
- Active version, installer and publish references are 0.2.2.1.

## Local verification required

Run the standard Build, Publish and Installer scripts on Windows with the .NET 8 SDK and Inno Setup installed. Verify sorting and grouping by Activity, standard exports, upgrade installation, and all Settings tabs.
