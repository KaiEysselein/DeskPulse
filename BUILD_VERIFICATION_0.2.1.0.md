# DeskPulse 0.2.1.0 Build Verification

## Applied changes

- All active version references updated to 0.2.1.0. Historical CHANGELOG entries were preserved.
- Removed the duplicated installer Additional Tasks startup option; the final post-install checkbox remains.
- Removed UserEvents from rule-based database housekeeping scan, deletion, progress totals, and result counts.
- User Activity grid now has one `Log` checkbox and a read-only event-name column.
- Existing old-style Exclude user-event rules migrate to unchecked Include rules.
- Existing user choices are no longer forcibly re-enabled; missing supported predefined events are added without overriding selections.
- User Activity Reset Defaults restores the full supported predefined list as checked.
- File Activity, App Activity, and Export Options reset handlers remain unchanged.
- Removed the obsolete hidden Maintenance Reset Defaults field, control creation, event binding, tooltip, and handler.

## Static checks

- No prior-version references remain in active files outside historical CHANGELOG entries.
- No obsolete installer task, user-event cleanup list, or hidden Maintenance reset symbols remain.
- Project XML files parse successfully.
- Installer source and publish/build scripts consistently reference 0.2.1.0.

## Compilation status

The source could not be compiled in this Linux artifact environment because the .NET SDK and Inno Setup compiler are not installed. Run `scripts\Build.ps1`, `scripts\Publish.ps1`, and `Installer\Build-Installer.ps1` on the Windows development machine before acceptance testing.
