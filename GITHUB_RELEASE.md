# DeskPulse Release Policy

## Permanent milestone releases

Permanent local archives and GitHub Releases are created only for versions matching:

`v0.x.0.0`

Examples:

- `v0.2.0.0`
- `v0.3.0.0`
- `v0.4.0.0`

These milestone folders are retained under the workspace-level `releases` directory.

## Intermediate builds

Intermediate builds such as `0.2.2.2` retain their exact application and installer version, but they are treated as replaceable development releases.

The latest approved intermediate installer is stored under:

`releases\current`

Building the next intermediate installer clears and replaces the contents of that folder.

Intermediate builds are not published as permanent GitHub Releases.

## Workspace layout

```text
DeskPulse\
├── dev\
│   ├── .git\
│   ├── src\
│   ├── scripts\
│   ├── Installer\
│   └── publish\
└── releases\
    ├── current\
    ├── v0.2.0.0\
    └── future milestone folders\
```

`dev\publish` remains temporary generated output and is ignored by Git.

## Installer archiving

`Installer\Build-Installer.ps1` always copies the completed installer into `releases\current`.

When the active version matches `0.x.0.0`, the installer is also copied into the corresponding permanent milestone folder.
