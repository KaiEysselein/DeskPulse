# DeskPulse Release Policy

## Repository layout

```text
DeskPulse\
├── .git\
├── README.md
├── CHANGELOG.md
├── ROADMAP.md
├── BACKLOG.md
├── HANDOVER.md
├── VERSION_CHECK.md
├── DATABASE_WRITE_AUDIT.md
├── GITHUB_RELEASE.md
├── LICENSE
├── dev\
│   ├── DeskPulse.sln
│   ├── Installer\
│   ├── Resources\
│   ├── scripts\
│   ├── src\
│   ├── docs\
│   └── publish\
└── releases\
    ├── current\
    └── retained milestone folders\
```

The Git repository root is `D:\Kai\GitHub\DeskPulse`.

GitHub-facing documentation remains at the repository root. Application source, build scripts, installer definitions, resources, and technical verification documents are contained under `dev`.

`dev\publish` is temporary generated output and is ignored by Git.

## Permanent milestone releases

Permanent local archives and GitHub Releases are created only for versions matching `v0.x.0.0`, such as `v0.2.0.0` and `v0.3.0.0`.

Milestone artifacts are retained under `releases\v<version>`.

## Intermediate builds

Intermediate builds retain their exact application and installer version but replace the contents of `releases\current`.

Intermediate builds are not retained as permanent GitHub Releases.

## Installer archiving

`dev\Installer\Build-Installer.ps1` always copies the completed installer into `releases\current`.

When the active version matches `0.x.0.0`, the installer is also copied into the corresponding permanent milestone folder.
