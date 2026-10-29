# ControlEvaluationPlan aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Catalog entry verified: `aggregates.json` owner `ControlEvaluationPlan` (retained baseline, polished 2026-10-29), backlog group 23.

## Boundary decision

**Keep together, narrow.** One stream per control: `Tenant / control-evaluation-plans / ControlId`, with an immutable `ProgramId` (the first accepted fact fixes it; a hydration for another program reports not found). No caller key forks the stream. `PlanVersionId` is the logical operation identity of one definition inside the stream. In the code it is derived from `(tenant, program, control, original expected version)` by `ControlEvaluationPlanIdentity`.

Named invariant: a plan version has a stable identity and an immutable procedure; only a successor to the current version can be appended (original expected version equals the current revision), and an exact duplicate identity has the same authored intent. The aggregate keeps only the latest fact. Older versions are immutable read composition (`ControlEvaluationPlanReader` plus verified native history), matching the catalog's "narrow" rationale. The procedure body lives in immutable content, so steps are validated together by `ControlEvaluationPlanRules` before the fact is raised.

Exclusions: approval and retirement of the control version belong to the control owner (admission check in the handler); evaluation start and freezing belong to the evaluation occurrence, which pins an exact plan version (D13-03); plan history lists are projections; authorization is the handler's.

History bound: the aggregate retains only `_latest`. `ControlEvaluationPlanHandlerTests.ShouldKeepEvaluationPlanAuthorityBoundedGivenManyDefinitionsWithHistoricalReads` already proves no retained collection after 32 definitions.

## Lifecycle

| State | Command | Result |
|---|---|---|
| empty line | Define, original expected 0 | `ControlEvaluationPlanVersionDefined` v1 |
| version n | Define, original expected n | `ControlEvaluationPlanVersionDefined` v(n+1), predecessor stays immutable |
| any | Define, expected not equal to current revision | conflict, no fact |
| any | Define with missing expected revision, foreign owner, other command kind | validation, no fact |
| any | Define with empty control version id | validation, no fact (was an exception; see Changes) |
| line of another program | Define / prepare | not found, no fact |
| any | same logical id, same authored intent | original result replayed by native acceptance, no fact |
| any | same logical id, changed intent | conflict by native acceptance, no fact |

There is no retire or disable lifecycle for a plan line; a change is always a successor.

## Changes

One real gap. `Define` with an empty `controlVersionId` reached `RaiseEvent`, and the replay guard in the reducer threw `InvalidOperationException` instead of returning a refusal. The command now returns a `Validation` failure before anything is raised (`ControlEvaluationPlan.cs`). In practice the handler never sends an empty id, since it first looks up the approved version, so this is a defensive command-level refusal.

The existing tests in `ControlEvaluationPlanTests.cs` exercise `ControlEvaluationPlanRules` only. The aggregate itself had no direct tests, so a new aggregate-only test class was added (`ControlEvaluationPlanAggregateTests.cs`).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks the stream | `ShouldKeepOneStreamPerControlGivenDifferentProgramsAndExpectedVersions` (new) |
| First version from empty line | `ShouldAppendFirstVersionGivenEmptyPlanAndOriginalExpectedVersionZero` (new) |
| Successor only at current revision | `ShouldAppendSuccessorGivenExpectedVersionEqualToCurrentRevision` (new) |
| Stale revision, competing decision, no fact | `ShouldRefuseAndAppendNothingGivenStaleExpectedVersion` (new); handler-level `ShouldFreezeExactPlanAndRejectOldVersionGivenPlanChangesAfterStart` (existing) |
| Invalid decisions append nothing | `ShouldRefuseAndAppendNothingGivenMissingOriginalExpectedVersion`, `...GivenOperationOfAnotherOwner`, `...GivenOperationOfAnotherCommand`, `...GivenEmptyControlVersion` (new) |
| Program fixed by first fact | `ShouldReportNotFoundGivenPlanLineOfAnotherProgram` (new) |
| Valid replay | `ShouldRestoreRevisionAndLatestGivenValidHistoryReplay` (new) |
| Exact retry after later facts, changed intent | `ShouldResolveOriginalAcceptanceGivenReplayAfterSuccessorAndRefuseChangedIntent` (new); `ShouldReplayOriginalPlanVersionGivenFreshTransportRequestAfterSuccessor` (existing, handler) |
| Procedure validity and exact scope pins | `ControlEvaluationPlanTests` (existing, rules) |
| Bounded history | `ShouldKeepEvaluationPlanAuthorityBoundedGivenManyDefinitionsWithHistoricalReads` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event add, rename or removal is needed. `ControlEvaluationPlanVersionDefined` (discriminator `bdgrz.control.evaluation.plan.version-defined`, version 2) carries tenant, program, control, control version, plan version id, version number, immutable content reference, operation provenance and the first-response and result references. It does not duplicate metadata.

Catalog discrepancy to reconcile by the catalog owner, not changed here: `aggregates.json` still lists the baseline typed fields (Objective, Steps, TesterIndependenceRequired, AuthoredByMemberId, Author, DefinedAt) while the shipped v2 event carries them inside `Content` and `Operation` (actor, accepted time). The catalog also says the plan version id is "passed from request"; the command contract has no such field and the id is derived from the original expected version. Both are `retained_baseline_business_facts_requires_schema_reconciliation` items.

## Later-stage follow-ups (not aggregate gaps)

- Reactor/handler ownership of "frozen once an evaluation starts": the evaluation occurrence pins the exact plan version and the handler refuses an old one (existing handler test).
- Projection of version history lists stays in `ControlEvaluationPlanReader`.
- Catalog schema reconciliation noted above.

## Evidence

Tests are written, not executed; build and run pending. No RED run was observed for the new empty-control-version test. No new Jev request was made for this slice. Helpers used (`NativeFacts.Root`, `Ref`, `Raised`, `Replay`, `AggregateScenario`) were read but not compiled.

## Review before build

Independent review of source and tests, read as compiler and analyzers; nothing built or run.

Findings: no correctness gap or compile risk found. Verified: `Define` refuses (Validation/Conflict/NotFound) before `RaiseEvent` on every invalid path; the fixed program is enforced via `PrepareDefine`; the reducer guard still protects replay. Helper signatures match `NativeFacts.Root(owner, Uuid, string, long?)`, `Raised`, `Replay`; `AcceptedOperationBinding.Of`, `Acceptance.SelectedProvenance/SelectedFact/Decide` and `NativeAcceptanceDecision` exist in `Bdgrz.Compliance.Features.NativeOperations.Models`; `DefineControlEvaluationPlan` is in `Evaluations.Commands`; PendingEvents is an array via `.ToArray()` with `.Length`; no event is re-attached; state asserted after replay is captured before further commands. Tests carry ordered Arrange/Act/Assert and valid names. Fixture values are Uuids and a 64-char hex sha; no string ids.

Jev (model jev-1.14.0, advisory), files `implementation-jev/controlevaluationplan-test-impl-review-2026-10-29-*.json`:

| Question | Answer | Probability |
|---|---|---|
| Derived PlanVersionId vs catalog "passed from request" | keep_derived_id_flag_catalog | 0.99 (confidence 0.98) |
| Derived id satisfies retry/changed-intent rule (Noul) | true | 0.71 |
| ProgramId in payload duplicates identity | keep_programid | 1.0 |
| Stale-version test asserts invariant (Noul) | true | 0.81 |
| Acceptance test asserts invariant (Noul) | true | 0.82 |

Disposition: derived id kept (decisive); catalog wording reconciliation is a catalog-owner item (no shared file edited). ProgramId stays. The 0.71 premise is moderate, so the retry claim rests on the existing handler test `ShouldReplayOriginalPlanVersionGivenFreshTransportRequestAfterSuccessor` and the new acceptance test.

Remaining risks: tests uncompiled; catalog reconciliation open; `ControlEvaluationPlanVersionDefined` event shape unchanged.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
