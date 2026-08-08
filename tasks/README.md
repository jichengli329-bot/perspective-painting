# Task handoff workflow

`ACTIVE.md` is the single short-lived handoff from Codex to Claude Code / DeepSeek.

1. Codex writes one bounded contract to `ACTIVE.md`.
2. The user keeps one Claude Code terminal session open in this repository.
3. The user sends only: `Execute tasks/ACTIVE.md. Start with the requested edit and follow CLAUDE.md.`
4. DeepSeek edits the named files and returns a short report.
5. Codex reviews the diff and performs acceptance checks.
6. Codex resets `ACTIVE.md` to `IDLE` or replaces it with the next contract.

Do not place API keys, full build logs, timestamps, or copied chat transcripts in this directory.
