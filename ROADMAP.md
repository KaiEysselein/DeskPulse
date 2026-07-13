# DeskPulse Roadmap

## Current development baseline

```text
0.1.3.2
```

Version `0.1.3.2` unifies File and Folder filtering into one File Activity wildcard model.

Completed in this version:

- removed the separate Folder Activity rules tab
- removed the duplicate Folder Activity View Log tab
- added path-aware `*`, `?`, and recursive `**` matching
- added **Add Folder...** to create one-level or recursive patterns
- migrated legacy folder rules to File Activity JSON
- advanced registry schema to version 5
- retained schema-1 rule-package import compatibility
- updated database housekeeping and documentation

## Verification before promotion

- compile and runtime-test locally
- test migration using a copy of real registry values
- verify direct-folder and recursive wildcard boundaries
- confirm App Activity executable precedence
- test View Log rule creation and cleanup
- test current-page XLSX export
- test full database cleanup and compaction

## Candidate next work

- move common glob matching into one reusable class used by monitoring, cleanup, and View Log rule previews
- add an optional rule-test panel showing which path a pattern matches
- add validation and visual warnings for malformed path patterns
- add database backup before destructive housekeeping
- improve asynchronous loading of Settings and View Log
- add cancellation to long database operations
- review installer and signed-release packaging
