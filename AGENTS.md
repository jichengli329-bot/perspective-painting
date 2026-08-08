# Perspective Puzzle collaboration rules

This repository is shared by the user, Codex, and Claude Code backed by DeepSeek V4 Flash.

## Roles

- User: product owner; decides feel, visual direction, and whether a build is enjoyable.
- Codex: product specification, architecture, task decomposition, repository research, review, and acceptance.
- Claude Code / DeepSeek: implementation, repetitive code generation, focused fixes, and local verification.

## Claude Code model configuration

- Use `deepseek-v4-flash` as the Claude Code implementation model.
- Set `CLAUDE_CODE_EFFORT_LEVEL=max` for implementation tasks.
- Do not use the `deepseek-v4-flash[1m]` model string in this environment; it caused Claude Code requests to hang during T-001.
- Codex should keep tasks narrow even at maximum effort so implementation remains reviewable and token usage stays controlled.
- Follow `docs/DEEPSEEK_EXECUTION_PLAYBOOK.md` when delegating implementation. Codex performs planning and verification in separate steps; DeepSeek receives microtasks with exact files, signatures, and forbidden scope.
- Reuse one Claude Code session for sequential project work so DeepSeek can reuse the exact conversation prefix. Do not spawn Agent Teams for routine implementation or verification.
- Allow bounded DeepSeek `max` calls up to 12–14 minutes, with a hard ceiling of 15 minutes, before treating long reasoning as a failure.

## Source of truth

Read these files before making changes:

1. `docs/PRODUCT.md`
2. `docs/ARCHITECTURE.md`
3. `TASKS.md`
4. `docs/DECISIONS.md`

Only implement the task marked `IN PROGRESS` in `TASKS.md`. Do not silently expand scope.

## Implementation rules

- Engine: Unity 6000.3.18f1, Universal Render Pipeline.
- Keep the core projection logic independent from scene objects and UI.
- Prefer small, reviewable changes.
- Do not add packages or copy code from public repositories without recording the source and license in `THIRD_PARTY_NOTICES.md`.
- Do not modify generated folders: `Library`, `Temp`, `Logs`, `Obj`, `Build`, or `Builds`.
- Do not rewrite working systems unless the active task explicitly requires it.
- Never commit credentials, API keys, local machine paths, or Unity license data.

## Task handoff

Codex writes the current bounded implementation contract to `tasks/ACTIVE.md`. Claude Code should receive a short instruction such as `Execute tasks/ACTIVE.md` in the existing project session; do not paste a newly reformatted copy of the contract into every turn.

When completing a task, update its entry in `TASKS.md` with:

- status;
- files changed;
- verification performed;
- known limitations;
- suggested next task.

If blocked, stop and record the exact blocker instead of inventing a workaround that changes the product.
