# ControlEvaluation aggregate review

## Identity, invariant and boundary

- **Identity:** tenant plus evaluation id (`control-evaluations` area). The start's `EvaluationId` is the caller-supplied immutable creation identity; a different id is a different evaluation, and a retest is a new evaluation naming (never rewriting) its predecessor's accepting review. Every later fact is a root operation of the same stream bound to an expected revision.
- **Named invariant:** one evaluation of one control moves in_progress -> submitted -> accepted (a rejected or changes-requested review reopens work in a new round) over a frozen plan and control version, at a strictly increasing logical revision, with tenant/program/control identity pinned by the start fact.
- **Exclusions:** step, conclusion, deviation and separation-of-duties content rules (`ControlEvaluationRules`, decided by the caller); the plan (`ControlEvaluationPlan`); findings, corrective actions and waivers (their owners); views (projections).
- **Decision:** keep as one aggregate. Steps, dispositions, submission and review of one round must be decided atomically against one revision and lifecycle; history is bounded by one evaluation's rounds. Different evaluations never contend.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | Start | `ControlEvaluationStarted` | already started (Conflict); non-root operation (Validation); replay-cut mismatch |
| in_progress | RecordStep / DisposeDeviation / Submit | `ControlEvaluationStepRecorded` / `DeviationDisposed` / `Submitted` | unstarted (NotFound); stale revision; not in_progress (Conflict) |
| submitted | Review | `ControlEvaluationReviewed` (accepted -> accepted; otherwise -> in_progress) | no submission (Conflict); stale revision; unknown decision (rules) |
| any | exact retry | none (original view) | changed intent (Conflict) |

## Changes

No source change: every done criterion was already satisfied. One coverage gap found and filled: a review with an unknown decision appending no fact.

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity / own stream | `ShouldUpdateDifferentEvaluationsConcurrentlyGivenSameControl`, `ShouldNotDiscloseGivenEvaluationFromAnotherTenantOrControl` |
| Success path | `ShouldFreezeProcedureAndExactControlVersionGivenOwnerStartsEvaluation`, `ShouldDeriveEffectiveOverallGivenEveryStepMet` |
| Invalid decision appends nothing | `ShouldRequireDeviationClassificationGivenNotMetStep`, `ShouldRejectConflatedConclusionsGivenMissingAssertionOrUnsupportedEffective`, `ShouldAppendNothingGivenMissingUnacceptedOrOtherControlPredecessor`, `ShouldAppendNothingGivenUnknownReviewDecision` (new) |
| No-op / exact retry after later facts | `ShouldReturnOriginalViewGivenExactStartRetryAfterLaterWork`, `ShouldReturnOriginalViewAndConflictOnChangedIntentGivenRetriesOfEveryEvaluationCommand`, `ShouldReplayEqualStepWinnerGivenLosingResultWriteUnavailable` |
| Changed intent | `ShouldConflictGivenSameEvaluationIdWithChangedActorOrIntent`, retries test above |
| Stale revision / competing | `ShouldRejectStaleRevisionGivenConcurrentStepRecord`, `ShouldRejectReviewGivenRevisionFromBeforeSubmissionAndSubmitGivenStaleRevision` |
| Lifecycle / rounds | `ShouldReopenWorkAndKeepSubmissionGivenRejection`, `ShouldKeepResultsForRevisionGivenChangesRequested`, `ShouldBlockSelfReviewGivenEvaluatorWithoutWaiver` |
| Retest | `ShouldBindRetestToExactPredecessorReviewAndLeavePredecessorUntouchedGivenRetestStart`, `ShouldFailClosedGivenRetestNamingAnotherFactThanTheAcceptingReview` |
| Replay / hydration | `ControlEvaluationReplayCutTests` (valid cut, legitimate later correction) |
| Bounded history | `ShouldKeepBoundedAuthorityGivenRepeatedStepCorrections`, `ShouldKeepEveryInspectedItemGivenFiftyMaximalItemsInOneStepResult` |

## Event contract status

`ControlEvaluationStarted`, `StepRecorded`, `DeviationDisposed`, `Submitted` and `Reviewed` carry business facts plus operation provenance and content references; no metadata duplication found. No event change needed.

## Later-stage follow-ups (not aggregate gaps)

- The aggregate trusts its caller for decision/disposition vocabulary (validated in `ControlEvaluationRules` before the aggregate runs); a handler-level guard is the existing seam.
- Material-deviation Finding creation and retest linkage belong to reactors/handlers.

## Evidence

Tests written, not executed; build and run pending. Nothing was built, formatted, committed or staged. No Jev call was made (no event or name change).

## Review before build

- **Findings:** the new test is sound. The review rules check revision, state, waiver and self-review before the decision vocabulary, and the test's reviewer (`ApproverUserId`) is not the evaluator, so the refusal reached is `InvalidContent`; sibling submit tests map `InvalidContent` to `RequestErrorKind.Validation` over HTTP. It asserts a stream fact count unchanged plus a failed result. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002; `Assert.Single` not needed; `.Count` is on a `List`. No source defect found; no fixes needed.
- **Jev (jev-1.14.0, advisory, 2026-10-29):** `unknown_asserts` Noul 0.90 (test proves invalid decision appends nothing); catalog discrepancy Choice `no_discrepancy` 0.95 (add_to_model 0.02, remove 0.01, needs_source_resolution 0.02). No discrepancy to resolve. Request and response in `implementation-jev/controlevaluation-test-impl-review-2026-10-29-*.json`.
- **Remaining risks:** the test is written, not executed; the Validation mapping is inferred from sibling tests and confirmed only at build/run.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
