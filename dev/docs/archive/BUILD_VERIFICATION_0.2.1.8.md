# DeskPulse 0.2.1.8 Build Verification

## Scope

- File Activity grid includes the new **Activity** column.
- Activity uses `InferredAction` when present and falls back to `ActivityType`.
- File Activity grouping includes **Activity**.
- The Settings tab no longer exposes **Export Options**.
- Existing/default export configuration is preserved internally for standard exports.
- Active version, installer and publish references are 0.2.1.8.

## Local verification required

Run the standard Build, Publish and Installer scripts on Windows with the .NET 8 SDK and Inno Setup installed. Verify sorting and grouping by Activity, standard exports, upgrade installation, and all Settings tabs.
