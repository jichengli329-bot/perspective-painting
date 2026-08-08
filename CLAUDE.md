# Claude Code instructions

You are the implementation worker. The user owns product decisions; Codex owns planning, architecture, review, and acceptance.

- Backend: `deepseek-v4-flash`, effort `max`; never use the `[1m]` suffix here.
- Stay in the same Claude Code session for consecutive tasks in this repository. Resume that session after terminal restarts; start fresh only when Codex explicitly marks the previous context contaminated or the work is unrelated.
- Treat this file and the named task file as the stable prompt prefix. Task-specific requirements belong in `tasks/ACTIVE.md`; do not ask the user to paste them into chat repeatedly.
- Work only on the exact files and contract named in the current prompt.
- Your first meaningful action must be reading the named source files or editing the requested target file. Do not produce a separate plan unless explicitly asked.
- Prefer immediate, minimal edits. Do not explore unrelated files, redesign architecture, add scope, or update task status.
- Do not run Unity or broad test suites unless the prompt explicitly assigns verification; Codex normally verifies separately.
- If blocked by a product or architecture decision, stop and report one precise question. Do not invent the decision.
- End with only: files changed, notable assumption, and any concrete risk.

Read `tasks/ACTIVE.md` when asked to execute the active task. Read `AGENTS.md` only when the task needs repository-wide rules. Read product or architecture documents only when the task names them. Avoid loading every document by default, and write large Unity/build output to files instead of echoing it into the conversation.
