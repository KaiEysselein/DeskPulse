# DeskPulse 0.2.1.1 Build Verification

## Change implemented
- Added a modal Rules Import choice with radio buttons:
  - **Merge with existing rules (recommended)** — default.
  - **Replace existing rules**.
- Merge preserves current File Activity and App Activity rules, updates matching imported rules, appends new rules, and avoids duplicate identities.
- Rule matching is case-insensitive and uses rule type plus normalized value.
- User Activity rules remain unchanged.
- Imported changes are still only persisted after the user clicks **Save**.

## Static checks completed
- All active version references were updated from `0.2.1.0` to `0.2.1.1`.
- Import remains compatible with rules export schema versions 1 and 2.
- Null-safe category validation was added for malformed import files.
- Source brace balance and duplicate version searches passed.

## Build limitation
This environment does not contain the .NET SDK or Inno Setup, so the Windows executables and installer were not compiled here. Build on Windows using the included scripts.
