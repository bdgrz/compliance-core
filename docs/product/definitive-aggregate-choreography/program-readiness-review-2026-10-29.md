# ProgramReadiness aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and shared files are untouched.

## Boundary decision

**Keep together.** `ProgramReadiness` is the readiness governor of one program. Identity is `AggregateIdentity.Create("ProgramReadiness", tenantId, programId)` on stream `{tenant}/program-readiness/{id}`; there is no creation key (the first run opens the stream), so no caller key forks a stream. The selected logical id only selects which operation to resolve (`NativeAcceptance`), never the stream.

Named invariant: a program has at most one pending latest-adoption generation and one pending management decision; a run reserves the next generation before any computation, a decision seals the latest adoption, and while either is pending the old latest cannot be decided and no second run is admitted. Only the latest assessment can take a decision, one readiness decision and one Type I entry decision per assessment.

Exclusions: assessment content and computation (`ReadinessAssessment`, immutable content), gap plans (`ReadinessGapPlan`), readiness rules and rule versions, runner/decider independence, held-gap-plan proofs, projections of latest assessment or decisions. Reducers never read content, another owner or a projection.

## Lifecycle

| State | Command | Result |
|---|---|---|
| any, no pending | RequestRun at current revision | `ReadinessAssessmentRequested`, reserves generation n+1 |
| any | RequestRun, stale revision or foreign/rootless operation | refusal, no fact |
| pending adoption or decision | RequestRun | transient conflict, no fact |
| pending adoption | Adopt exact root, generation and intent | `ReadinessAssessmentAdopted`, becomes latest, decisions reset |
| pending adoption | Adopt other generation/intent, or nothing pending | conflict, no fact |
| latest adopted, nothing pending | AcceptDecision (readiness or Type I entry) at current revision | `ReadinessDecisionAccepted` / `TypeIEntryDecisionAccepted`, seals latest generation |
| any | AcceptDecision for unknown assessment, stale revision, older assessment, already decided kind | refusal, no fact |
| decision pending | Record exact decision of latest assessment | `ReadinessDecisionRecorded` / `TypeIEntryDecisionRecorded` |
| decision pending | Record other root, intent, kind or assessment | conflict, no fact |
| decision pending | CancelDecision exact root | `ReadinessDecisionCanceled`, fences delayed completion; assessment may be decided again |
| any | same logical id, same intent | replay of original acceptance (`NativeAcceptance`) |
| any | same logical id, changed intent | conflict, no fact |

## Changes

One real gap found and fixed. `RecordReadinessDecision` and `RecordTypeIEntryDecision` validated the pending decision's root, intent and owner but not the caller-supplied `assessmentId`. A mismatching id passed the command check and failed only in the reducer with `InvalidOperationException`, so a bad command surfaced as a crash instead of a refusal. `PendingFailure` now also requires `assessmentId` to equal the latest adopted assessment and refuses with a non-retryable conflict, appending no fact.

Tests: new `ProgramReadinessTests.cs` (aggregate level; before this file the owner was covered only through handler, history and HTTP tests).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveOneIdentityGivenSameTenantAndProgram` |
| Run admission, reserved generation | `ShouldReserveOnePendingGenerationGivenFirstRun` |
| Stale revision / invalid run appends nothing | `ShouldAppendNothingGivenRunWithStaleRevision`, `ShouldAppendNothingGivenRunOwnedByAnotherStream` |
| No second run while pending | `ShouldRefuseSecondRunWithoutFactGivenPendingAdoption`, `ShouldRefuseRunAndSecondDecisionWithoutFactGivenPendingDecision` |
| Exact adoption only | `ShouldRefuseAdoptionWithoutFactGivenGenerationOrIntentOfAnotherRun`, `ShouldRefuseAdoptionWithoutFactGivenNothingPending`, `ShouldMakeAssessmentLatestAndClearPendingGivenExactAdoption` |
| Decision admission, seal | `ShouldSealLatestAdoptionGenerationGivenAcceptedDecision`, `ShouldRefuseDecisionWithoutFactGivenUnknownAssessment`, `ShouldRefuseDecisionWithoutFactGivenStaleRevision` |
| Only latest assessment decidable | `ShouldRefuseDecisionOfOlderAssessmentWithoutFactGivenLaterAdoption` |
| One decision per kind per assessment | `ShouldRefuseSecondReadinessDecisionWithoutFactGivenAlreadyDecidedAssessment`, `ShouldRefuseSecondTypeIEntryWithoutFactGivenEntryDecisionOnAssessment`, `ShouldKeepReadinessDecisionAndAllowTypeIEntryGivenDecidedAssessment` |
| Exact completion | `ShouldRecordReadinessDecisionAndFreePendingGivenExactCompletion`, `ShouldRefuseCompletionWithoutFactGivenNothingPendingOrChangedIntent` |
| Gap fixed: assessment mismatch refused | `ShouldRefuseCompletionWithoutFactGivenAssessmentOtherThanLatest` (RED against the old code) |
| Cancel fences delayed completion | `ShouldCancelPendingDecisionAndFenceCompletionGivenExactFailure`, `ShouldAllowNewDecisionOnSameAssessmentGivenCanceledDecision` |
| New adoption resets decisions | `ShouldResetDecisionsGivenLaterAdoption` |
| Valid replay | `ShouldRestoreStateAndRefuseRunsGivenValidHistoryReplay` |
| Exact retry after later facts | `ShouldReplayCommittedRunGivenExactRetryAfterLaterFacts` |
| Changed intent | `ShouldConflictGivenSameLogicalRunWithChangedIntent` |
| Bounded history | `ShouldRetainBoundedStateGivenManyRunsAndDecisions`; existing `ShouldRetainBoundedCommandAuthorityGivenManyHistoricalReadinessWrites` |

Malformed-history tests were not added, by user preference. Existing handler/process level tests (`ReadinessNativeHistoryTests`, `ReadinessAssessmentHandlerTests`, `ReadinessNativeHttpTests`) remain the end-to-end coverage.

## Event contract status

Events carry only business facts and operation provenance; no metadata duplication found. One open item keeps the aggregate **partial**:

- The catalog invariant names a "permanent approved Type I entry pointer". The aggregate does not hold it: `_latestEntryDecision` is reset on every adoption, and the "program already entered Type I" refusal is decided in `ReadinessDecisionRules`/`ReadinessDecisionAdmission` from `ReadEntryDecisionsAsync`, a projection read. The reducer cannot hold the pointer because `TypeIEntryDecisionRecorded` carries the outcome only inside immutable content (`DecisionContent`), and reducers must not read content. Needed event change: add a business `Outcome` string (`approve`, `approve_with_exceptions`, `defer`) to `TypeIEntryDecisionRecorded` (and, optionally, `Outcome` to `ReadinessDecisionRecorded`). Then `ProgramReadiness` can keep a non-defer entry pointer and refuse `AcceptDecision(TypeIEntry)` itself. Not applied here (shared event/schema files).

Behavior noted, not changed: a deferred entry decision still consumes the per-assessment entry slot ("run a new assessment"); it does not consume the program-wide approved slot.

## Later-stage follow-ups (not aggregate gaps)

- Admission and completer should drop the `ReadEntryDecisionsAsync` entered-Type-I check once the aggregate holds the pointer.
- Handler/reactor wiring of `Adopt`, `CancelDecision` and decision completion is unchanged.

## Evidence

Tests are written, not executed; build and run pending. No Jev request was made for this slice. Test helpers (`TenantFacts.Provenance`, `TenantFacts.Response`, `DomainEventSeed.Attach`, `AggregateScenario`) were read but not compiled.

## Review before build

Independent review of source, tests and this file. Nothing was built or run.

Findings and fixes:

1. Test defect (fixed). `ShouldRefuseDecisionOfOlderAssessmentWithoutFactGivenLaterAdoption` asserted `Conflict`, but with an unselected older assessment `PrepareDecision` fails first on `AdoptionOf(...) is null` and returns `MissingRecord`, which maps to `NotFound`. The test would have failed. It now asserts `NotFound`. The "A later assessment exists" `StateConflict` branch is reachable only when the hydration selects the older assessment, so a new test `ShouldRefuseDecisionAsConflictWithoutFactGivenSelectedOlderAssessmentAfterLaterAdoption` seeds two run/adoption pairs, selects the first assessment, and asserts `Conflict`, no pending decision and unchanged revision.
2. Compile hardening (fixed). The tuple assertion on `DecisionTerms` now compares against `.Value` so both sides are the same non-nullable tuple type.
3. Implementation: the `PendingFailure` assessment check is correct; a refused completion appends nothing and keeps the decision pending. No metadata duplication found in the payloads. A deferred entry decision consuming the per-assessment slot matches the rule.
4. Unconfirmed compile risks: `AggregateScenario.PendingEvents` indexing with `[^1]`, `Acceptance.SelectedFact` and `Acceptance.Decide`, and `DomainEventSeed.Attach` were taken from sibling tests and not compiled.

Jev (model jev-1.14.0; files `implementation-jev/programreadiness-test-impl-review-2026-10-29-*.json` and `programreadiness-discrepancy-2026-10-29-*.json`):

| Question | Answer |
|---|---|
| Mismatch completion test proves wrong-assessment completion cannot append | noul 0.89 |
| NotFound for unselected older assessment is consistent with latest-only decidability | consistent 0.80 (inconsistent 0.19) |
| Deferred outcome consuming the assessment entry slot is consistent | consistent 0.99 |
| Cancel test proves late completion refused and new decision allowed | noul 0.94 |
| Premise: pointer cannot be held without an outcome field | noul 0.84 |
| Disposition for missing outcome on TypeIEntryDecisionRecorded | add_to_model 0.93 (remove 0.03, needs_source_resolution 0.04) |

Disposition: decisive, add the `Outcome` business field to `TypeIEntryDecisionRecorded` in the catalog and schema. This touches shared event files, so it is not applied here and stays in the remaining list. The aggregate stays partial on the permanent approved Type I entry pointer.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
