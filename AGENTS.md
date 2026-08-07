# DeskPulse Codex / AI Working Instructions

These instructions apply to AI-assisted development work in this repository.

## Scope

- Keep tasks narrowly scoped to the user's request.
- Inspect only the files required for the requested task.
- Do not perform repository-wide audits unless explicitly requested.
- Do not make unrelated refactors, formatting changes, renames, or cleanup.
- Preserve existing architecture and behaviour unless the task requires a change.

## Local working files

- Use `D:\Kai\GitHub\DeskPulse\Temp` for temporary files, downloads, extracted files, generated files, intermediate artifacts, disposable scripts, and other AI-assisted working material.
- Do not use the Windows user's general Downloads folder for DeskPulse project work.
- Use `D:\Kai\GitHub\DeskPulse\Backups` for persistent local backups and recovery copies.
- Never commit or intentionally stage `Temp/` or `Backups/`.

## Safety

- Do not modify credentials, signing material, certificates, private keys, secrets, tokens, or other sensitive configuration unless explicitly requested.
- Do not remove or stop tracking project or IDE files without first establishing that they are unnecessary.
- Do not use force-push blindly.
- If local and remote history diverge, or the remote appears to have been force-updated, inspect the history before taking corrective action.

## Validation

- Run only tests, builds, or checks relevant to the files and behaviour changed.
- Before every commit or push, inspect `git status`, ignored files where relevant, and the relevant diff.
- Keep changes in small, logical commits.

## Final response

Keep completion summaries concise and include:

- files changed;
- checks or tests run;
- any unresolved issues or follow-up work.
