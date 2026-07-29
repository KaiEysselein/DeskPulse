# DeskPulse release channels

DeskPulse publishes two GitHub release streams.

## Stable

- Numbered immutable tags and releases, such as `v0.4.0.0`.
- Triggered manually with the **Stable release** GitHub Actions workflow.
- The workflow requires an existing tag that exactly matches the four-part numeric version.
- Existing stable releases are never overwritten.
- Stable installers are intended for ordinary use and become GitHub’s latest release.
- Local stable acceptance continues to include installation, live database checks and ESET scanning before the tag is approved.

## Nightly

- One moving `nightly` tag and one **DeskPulse Nightly** prerelease page.
- Built automatically from `main` every day at 02:15 UTC, or manually on demand.
- The release assets are replaced on each successful run:
  - `DeskPulse_Setup_Nightly.exe`
  - `DeskPulse_Setup_Nightly.sha256`
- Nightly binaries retain the current numeric Windows file version and add a date, workflow run and commit to their informational version.
- Nightly builds run the complete automated test suite and a Microsoft Defender scan on the GitHub Windows runner.
- Nightly builds are intended for testing and upgrade the same DeskPulse installation as Stable.

The Nightly stream does not replace the Stable acceptance process. Stable releases remain the only channel receiving the full local installation, protected-database and ESET verification recorded in `VERSION_CHECK.md`.
