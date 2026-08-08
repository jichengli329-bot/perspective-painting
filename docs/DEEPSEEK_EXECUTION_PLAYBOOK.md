# DeepSeek V4 Flash execution playbook

This project uses Codex as planner/reviewer and Claude Code backed by DeepSeek V4 Flash as a focused implementation worker.

## Why this workflow

V4 Flash is strong at bounded coding and tool use, especially when the intended diff is already known. Maximum reasoning effort improves the ceiling on difficult agent tasks, but it has no precise thinking-token budget. Broad or ambiguous prompts can therefore spend a long time evaluating approaches before making a file edit.

The remedy is not a larger prompt. It is to remove decisions from the implementation prompt.

## Required split of responsibilities

### Codex does before delegation

1. Resolve product and architecture choices.
2. Inspect relevant current files.
3. Define exact target files and public signatures.
4. State invariants, edge cases, and forbidden scope.
5. Define a deterministic verification command or artifact.

### DeepSeek does in one implementation call

1. Read only the files named in the prompt.
2. Edit only the named target files.
3. Implement the supplied contract.
4. Report changed files, one assumption, and concrete risks.

### Codex does after delegation

1. Review the actual diff rather than the model report.
2. Run Unity compilation/tests or render capture.
3. Feed exact compiler/test errors back as a new, clean repair task.
4. Mark task status only after independent verification.

## Task sizing

A Flash implementation call should normally satisfy all of these:

- one responsibility;
- one to three target files;
- signatures already specified;
- no product decision;
- no package selection;
- no simultaneous implementation and broad verification;
- expected diff small enough to review in one pass.

Split examples:

- Bad: build the complete visual prototype, create meshes/materials/scenes, render it, test it, and update documentation.
- Good call 1: implement `RoundedBoxMeshFactory.Create(Vector3,float,int)` in one named file using the supplied algorithm.
- Good call 2: implement one editor scene builder using the already-tested mesh factory and named palette values.
- Good call 3: implement one batch screenshot method with a specified output path.
- Verification: Codex invokes Unity separately and returns exact errors if repair is needed.

## Prompt template

```text
MODE: IMPLEMENT, not plan or research.

Read only:
- <specific file>

Edit only:
- <specific target file(s)>

Contract:
- <exact type and method signatures>
- <required behavior and edge cases>
- <algorithm or existing pattern to follow>

Forbidden:
- no other files
- no dependencies
- no architecture changes
- no documentation or task-board edits
- no broad test run

Start by editing the target file. Do not write a plan first.
Stop after the requested diff exists.
Return only: files changed; assumption; concrete risk.
```

## Reasoning effort policy

- Keep `max` for bounded implementation tasks where correctness matters and the contract is already fixed.
- If a task is purely mechanical and latency matters, `high` is the sensible fallback because it is V4 Flash's normal default.
- Never use max as a substitute for a precise specification.
- Maximum-effort calls may legitimately reason for several minutes before the first edit. Allow up to 12–14 minutes for a bounded task; use 15 minutes as the hard ceiling unless the user explicitly changes it.
- During that window, inspect process health and file changes without interrupting the call. Stop early only on a concrete error, an invalid repeated tool loop, or clear scope drift.
- If a bounded max call reaches the 15-minute ceiling without a usable diff, stop it, shrink the task, or add signatures/algorithm. Do not expand context as the first remedy.

## Session and context policy

- Keep one persistent Claude Code session for consecutive tasks in this repository. Continue interactively or resume the same session after a terminal restart.
- Start a fresh session only for unrelated work, after severe tool-output contamination, or when Codex explicitly requests a clean context.
- Put the changing implementation contract in `tasks/ACTIVE.md`. Invoke it with the stable, short instruction `Execute tasks/ACTIVE.md` instead of rebuilding a large prompt on every call.
- Do not make the worker read every product document on each call.
- Keep `CLAUDE.md` short; task-specific detail belongs in the prompt or active task specification.
- Keep static instructions first and dynamic task details last. DeepSeek caching only helps when the request matches from the first token.
- Store verbose Unity output under `Logs` or `outputs`; refer to the path rather than pasting changing logs into the conversation.
- Avoid routine Agent Teams and subagents: every extra session creates a separate cache lane.
- Preserve reasoning and tool-call history inside the project session while it remains relevant; compact or restart only when accumulated output is clearly harming execution.

## Verification policy

Every delegation needs a pass/fail signal, but verification is normally a separate Codex step:

- C# domain change: targeted EditMode tests.
- Unity editor tool: batch compilation and exact exit code.
- Visual change: deterministic 1280x720 capture and image inspection.
- Bug fix: a previously failing focused test or compiler error must pass.

Model claims such as "implemented" or "should compile" are not acceptance evidence.
