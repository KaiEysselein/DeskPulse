# DeskPulse 0.2.1.2 Build Verification

## Applied source changes

- Active version references updated to 0.2.1.2.
- Settings footer changed to Save, Save and Close, and Close.
- Close/X/Esc use the same unsaved-change confirmation.
- Tray menu actions are deferred until the menu closes, restore an existing matching form, and are intended to respond on the first click.
- Exit tray renamed to Quit DeskPulse.
- Database housekeeping uses a streaming named-pipe response with PROGRESS and RESULT messages.
- Service-side housekeeping reports File Activity scan, App Activity scan, matching counts, deletion thresholds, and SQLite compaction.
- Deletion UI updates are throttled to each 10% threshold.

## Environment limitation

The Linux artifact environment does not contain the .NET SDK or Inno Setup, so Windows compilation and installer generation could not be executed here. Compile and test on the normal Windows development machine.
