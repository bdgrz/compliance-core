---
name: aggregate-batch-workflow
description: Run a fast multi-agent batch over several compliance-core aggregates - parallel implement, then Jev review, with no build until both finish - then build, test, record evidence and commit one aggregate at a time. Layers on aggregate-implementation-jev; same scope lock.
---

# Aggregate batch workflow

Use when the user asks for a fast, parallel, multi-agent aggregate session ("batch", "fan out", "multi agent coding session"). This is the **Workflow** tool, which needs explicit user opt-in; do not start it unprompted. It changes *how* a batch of aggregates is worked, not *what* is in scope: every agent obeys `aggregate-implementation-jev` (domain events and aggregates only; no handlers, reactors or projectors) and `AGENTS.md`.

## 0. Preconditions (check before launching)

1. **Green baseline.** `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore` has 0 errors. Do not start a batch on a red build; agents cannot tell their breakage from existing breakage.
2. **Clean or understood tree.** `git status`; know which uncommitted work is not yours. Agents never touch git, but the later commits must not sweep it in.
3. **Batch list.** 2-4 aggregates per batch is the target for normal work; the user may ask for more (the 2026-10-28 run used 10 in one workflow). Pick in dependency order from `docs/product/definitive-aggregate-choreography/aggregate-backlog.md`. Per aggregate collect: source file, own test file(s), review file (the `review` path given per aggregate), and a hint (known-failing tests, uncommitted prior work). Get known failures from the last full test log.
4. **Jev key** exists at `~/.config/typesafe/jev.key` (never print it).

## 1. The workflow

Script template: `references/workflow-template.js`. Pass the batch as `args` (JSON array of `{name, src, tests[], review, hint}`); read the template first, do not rewrite it from memory. It is a `pipeline()` of two stages per aggregate, no barrier between them:

- **Implement** (`phase: Implement`): one agent qualifies the aggregate against the done criteria, writes the failing test for each real gap, fixes it minimally, updates its review file. Returns a structured summary including `eventChangesNeeded` (reported, never applied).
- **Jev review** (`phase: Jev review`): an independent agent re-reads the actual diff as a skeptic (implementation, tests, compile risks), asks Jev 2-5 atomic questions, fixes confirmed findings, saves the request/response JSON under `implementation-jev/` beside each review file, and appends a "Review before build" section.

Hard rules baked into both prompts (keep them):
- **No** `dotnet build/test/format/restore`, **no** git writes. Build and test happen once, after review.
- Edit only the aggregate's own source, own tests, own review file; new files only if aggregate-private.
- Never edit shared files: the tracker, `tools/domain-events/**`, `ComplianceCoreJsonContext.cs`, event catalogs (`docs/product/definitive-aggregate-choreography/events.*`), shared test support, other owners' files. Parallel writers would clobber them. Event add/rename/remove is reported, then applied by the coordinator in a separate step.
- Never print or commit the Jev key; run the call as `TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node script.mjs`.
- **Discrepancies are resolved with Jev, not left open** (user rule, 2026-10-28): model-vs-code event gaps, name conflicts, payload fields that repeat stream identity, owner decisions the model is silent on. The platform keeps no private execution records, so a gating fact is a model gap to register. Reviewers ask a Choice plus a Noul, apply decisive answers to their own aggregate, and report model/schema/generated-code changes for the coordinator's single-writer contract step.
- Jev is advisory. Near-even or low-confidence answers are unresolved, not approval; Jev never overrides the approved model.
- Do not change analyzer rules or their enforcement to make anything compile or run faster.

## 2. After the workflow (coordinator does this inline)

1. Skim each result: `needs-attention` verdicts, `eventChangesNeeded`, `remaining`. Apply any event changes yourself via `tools/domain-events` (single writer), then regenerate.
2. **Build once**: `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore` (run it in the background with output to a log; builds can exceed the foreground limit). Fix compile errors. Blind-written code compiled first time on the last run, but expect to fix some.
3. **Test the batch**, then the full suite: `dotnet test ... --no-build -c Release [--filter ...]`. Diff the failure list against the previous full-run list; investigate every *new* failure. Test-side defects from blind writing are common (invalid fixture values such as a non-UUID realm); fix the test, not the aggregate, when the aggregate behavior matches the model.
4. **Format** only the changed files: `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>` (background, log to a file). Whole-solution `dotnet format` can run for 10+ minutes.
5. **Record evidence** in each review file: exact commands and results, "RED not observed" for blind-written tests, Jev model/probabilities, and the tracker row (Done only when the done criteria hold; Partial with the named open item otherwise).
6. **Commit one aggregate at a time**, staging explicit paths. The tracker is a shared file: stage it cumulatively so each commit carries only its own row (see the commit script pattern in `references/workflow-template.js` notes). A baseline/infrastructure commit goes first. Commit messages follow `AGENTS.md` (`feat(scope): summary (#issue)`). Commit and push only when the user has asked.

## 3. Lessons from the first run (coco-platform-core, 2026-10-28)

- Time figures from the platform-core run (50 s build, 8-14 s for ~6,700 tests) are illustrative only; measure this repo. `timeout` does not exist on macOS; use background runs with a log file instead.
- A hung test stalled a run for over an hour. `test/test.runsettings` (wired in `test/Directory.Build.props`) aborts after 60 s of inactivity and writes a `Sequence_*.xml` naming the in-flight test.
- Editing `Directory.Build.props`-style shared files: read first. A `cat >` overwrote an existing `test/Directory.Build.props` once (recovered from git).
- - Handler, projector and execution tests fail for reasons outside the aggregate stage; record them, do not repair them in an aggregate round.
- Agent-claimed "new tests" can already be committed; trust `git diff`, not summaries.
