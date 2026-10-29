# Repository Guidelines

## Project Structure & Module Organization

This is the backend repository. `Compliance.slnx` contains the .NET projects: `src/Compliance.Common` holds backend contracts and primitives, `src/Compliance.Core` owns domain behavior and Portia/Fitz integration, and `src/Compliance.App` hosts the API and worker. Dependencies point inward: App → Core → Common. .NET tests live in `test/Compliance.Tests`, backend API contract tooling lives in `tools/`, and product and architecture decisions live in `docs/`.

The web client is maintained in [bdgrz/compliance-web](https://github.com/bdgrz/compliance-web); the iOS and Android clients are maintained in their own repositories. Client implementation and client-specific tests belong in the owning repository. This backend owns API behavior, authorization, and the OpenAPI contract. Client repositories consume a pinned contract or generated client; they must not depend on `Compliance.Common` or reproduce backend authorization rules.

## Build, Test, and Development Commands

Use the .NET SDK selected by `global.json` and Docker Compose. Copy `.env.example` to `.env` and configure package-registry credentials before restoring .NET packages.

- `dotnet restore Compliance.slnx`: restore .NET dependencies.
- `dotnet build Compliance.slnx -c Release --no-restore`: compile with analyzers and warnings as errors.
- `dotnet test Compliance.slnx -c Release --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: run the backend unit-test suite.
- `docker compose up --build`: start the standalone app and its dependencies at `http://127.0.0.1:8080`.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, LF, final newline, four-space C# indentation, and two-space JSON/YAML/XML indentation. Use file-scoped C# namespaces, one top-level type per matching file, `PascalCase` types and members, `I` interfaces, and `_camelCase` private fields. HTTP route/query names and JSON properties use `snake_case`. Check C# whitespace formatting with `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore`; the .NET build enforces configured style and analyzer diagnostics.

## Testing Guidelines

Backend tests use xUnit. For every backend behavior change, first add a focused failing unit test. Name .NET tests `Should<ExpectedBehavior>Given<Condition>` (optionally prefix the method); mark Arrange, Act, and Assert in block-bodied tests.

## Fast Development Loop

1. **Red:** write a focused backend unit test that demonstrates the missing behavior and run that test alone.
2. **Green:** make the smallest backend change that passes the focused unit test. Run the backend unit-test suite when the change is ready for review.
3. **PR:** open a focused backend PR after the unit-test suite passes. CI runs the repository's applicable format, build, and unit-test checks.
4. **Adversarial review:** review the exact PR head for contract, authorization, tenant isolation, replay, and failure-mode gaps. Treat review findings as actionable until resolved or explicitly documented.
5. **Refine and refactor:** address findings, simplify the implementation, and rerun the focused test plus the unit-test suite. Push the revised head and repeat review as needed.
6. **Squash merge:** merge after the final PR head has passed required CI and the adversarial review is clear.

For backend features, current acceptance is proved with focused and full unit tests. End-to-end validation is a later phase, after backend and client features work independently.

## JEV and Backlog Workflow

JEV is TypeSafe's Jev model, called through the TypeSafe System One API. It supplies semantic judgments for backlog triage; GitHub Issues and the `Compliance — SOC 2 product journey` project remain the source of truth for issue text, status, dependencies, and delivery progress. Use `scripts/backlog-jev.mjs` as the repository wrapper. It reads GitHub and calls JEV but does not change GitHub state; use the GitHub CLI to record issue comments, dependencies, project fields, and completion.

The API key is stored at `~/.config/typesafe/jev.key`. Load it only into the command environment, and never print, commit, or copy the key into a file:

```sh
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs triage --milestone R1
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs criteria 209 211
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs preflight 437
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs api-gap "<client API need> :: <current workaround>"
```

Run `triage` to select and group work, `criteria` before closing an issue, and `preflight` on the proposed PR head. Treat model probabilities as review signals: verify them against issue text, recorded dependencies, decisions, code, unit tests, and CI. Record newly discovered blockers in GitHub before proceeding. Backend API gaps belong in this repository; client implementation belongs in the web or mobile repository that owns the experience. Link cross-repository issues and coordinate contract compatibility instead of combining code from multiple repositories into one PR. See `docs/product/delivery-cycle.md` for backlog queue rules.

## Aggregate Implementation Workflow

Aggregate work follows the repo skills in `.agents/skills/` (also linked from `.claude/skills/`):

- `aggregate-implementation-jev`: one aggregate at a time, invariant-first. Qualify identity, the named invariant, lifecycle and the keep/split/reactor decision before coding; write behavioral RED tests; make the smallest change to GREEN; review with Jev; record evidence in the aggregate's review file and `docs/product/definitive-aggregate-choreography/aggregate-backlog.md`.
- `aggregate-batch-workflow`: a fast, parallel multi-agent session over several independent aggregates. It needs explicit user opt-in ("batch", "fan out"). Agents implement and Jev-review without building; the coordinator then applies shared-file event changes as the single writer, builds once, tests, formats changed files, records evidence, and commits one aggregate at a time with explicit paths.
- `jev-prioritization`: how to ask Jev for ranking or design judgment calls beyond `scripts/backlog-jev.mjs`.

Rules for aggregate rounds:

- Scope is domain events and aggregates only. Handlers, reactors, projectors and API routes are later stages that need an explicit instruction; record them as follow-ups instead of building them. "Proceed" and "next aggregate" authorize aggregate work only.
- An aggregate owns one named business invariant decided atomically. Keep facts together only when one owner must decide a rule over them atomically; split independently governed members, and treat parent rosters, inboxes and histories as projections. Any split must preserve the original invariant through authoritative admission or explicit coordination, never a projection check.
- Mark an aggregate done only when identity, invariant, lifecycle, event contracts and focused tests (refusal with no fact, no-op, exact retry versus changed intent, replay, competing appends) are in place. Otherwise mark it partial with the named open item. Handler wiring, framework durability and full-suite status are not aggregate completion conditions.
- Resolve model, code and naming discrepancies with Jev instead of leaving them open; apply decisive answers and record the probabilities. Near-even or low-confidence answers stay unresolved. Jev is advisory and never overrides the approved catalog, source authority or aggregate boundaries.
- Parallel agents edit only their own aggregate's source, tests and review file. Shared files (the backlog tracker, `tools/domain-events/**`, generated JSON contexts, event catalogs, shared test support) have a single writer: the coordinator.
- Build and test once after a batch, not per agent. Never change analyzer rules or their enforcement to make a build or test run faster. Trust `git diff` over agent summaries.
- Stage explicit paths per aggregate commit so unrelated worktree changes are never swept in.

## Commits & Pull Requests

Recent commits use `feat(scope): summary (#issue)` or `docs(scope): summary (#issue)`. Keep each branch reviewable and link its backend issue. A PR in this repository changes backend code or its API contract; link related client work rather than including client code. Complete the PR template’s summary, validation, and contract/operations sections, including authentication, wire compatibility, persistence, and deployment effects where relevant. Run the fast development loop above and require CI on the final PR head before squash merge. This repository's CI runs formatting, build, and backend unit tests. See `CONTRIBUTING.md` for branch and release rules and `SECURITY.md` for vulnerability reporting.
