# AccessReviewDecisionBatch aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain the selected split from `AccessReviewCampaign`).** One batch (stream `access-review-decision-batches/{id}` under the tenant) decides atomically that one admitted bulk decision is either published as one all-item decision or cancelled, never both.

- Identity: tenant + campaign id + batch id (`AccessReviewEffects.BatchStreamId`). The batch id is also the root operation of the materialization. No caller request key forks the stream; receiver `CommandId` is only the selected logical operation for replay.
- Invariant: one immutable intent (preview/item selection and shared rationale, held as verified content) per batch, and one terminal publication decision: publish once, or cancel (accept then complete), never after the other. Partial item application stays unpublished; item holds, provisional decisions and fences live on item streams.
- Exclusions: item state, campaign revision (the batch has its own revision: 0 materialized, 1 published or cancellation accepted, 2 cancelled), reviewer/remediation separation, preview computation.

## Lifecycle

| State | Command | Event | Revision |
| --- | --- | --- | --- |
| none | Materialize | AccessReviewDecisionBatchAccepted | 0 |
| materialized | Publish | AccessReviewDecisionBatchPublished | 1 |
| materialized | AcceptCancellation | AccessReviewDecisionBatchCancellationAccepted | 1 |
| cancellation accepted | CompleteCancellation | AccessReviewDecisionBatchCanceled | 2 |

Publication and cancellation exclude each other; both terminal states refuse every further command.

## Changes

- Gap: `Publish`, `AcceptCancellation` and `CompleteCancellation` did not check that the operation provenance is on the batch's own stream, and `Materialize` did not check the root is the batch id. A mismatch reached the reducer and threw `InvalidOperationException` from `RaiseEvent` instead of a refusal. The commands now return `Validation` before raising (same guard `Materialize` already had for the owner).
- No other source change; the exact-retry and changed-intent decision stays with `NativeAcceptance` and the handlers' `Decide`.

## Test-to-criterion table (`AccessReviewDecisionBatchTests`)

| Criterion | Tests |
| --- | --- |
| Stable identity | `ShouldDeriveOneStreamGivenSameTenantCampaignAndBatch` |
| Materialize happy path, revision zero | `ShouldMaterializeAtRevisionZeroGivenFreshBatch` |
| Refusal with no fact (foreign stream, wrong root, duplicate) | `ShouldAppendNothingGivenMaterializationOnForeignStreamOrRootedElsewhere`, `ShouldAppendNothingGivenSecondMaterialization`, `ShouldAppendNothingGivenFactOnForeignStream` |
| Publish and exclusivity | `ShouldPublishAtRevisionOneGivenMaterializedBatch`, `ShouldAppendNothingGivenPublicationBeforeMaterialization`, `ShouldAppendNothingGivenSecondPublicationOrPublicationAfterCancellation` |
| Cancellation lifecycle | `ShouldAcceptAndCompleteCancellationGivenMaterializedUnpublishedBatch`, `ShouldAppendNothingGivenCancellationBeforeMaterializationAfterPublicationOrRepeated`, `ShouldAppendNothingGivenCompletionWithoutAcceptedCancellationOrRepeated` |
| Replay of valid history | `ShouldRestoreStateGivenValidPublishedHistory`, `ShouldRestoreStateGivenValidCanceledHistory` |
| Exact retry after later facts, changed intent | `ShouldReplayOriginalMaterializationGivenExactRetryAfterPublication` (Replayed vs Conflict) |
| No selection for unrelated logical id | `ShouldSelectNothingGivenUnrelatedLogicalIdOnHydration` |
| Stale revision / competing appends | Portia expected-position append OCC (framework); handlers retry on `EventStreamConcurrencyException`. Existing process tests in `AccessReviewBulkDecisionTests` exercise the flow end to end. |

Malformed-history tests were not added, by user preference.

## Event contract status

- Accepted, Published, CancellationAccepted and Canceled carry owner, revision, generation, intent, optional proof refs, provenance and result refs; no metadata duplication. No add, rename, remove or payload change requested.
- `AccessReviewDecisionBatchAdmitted` belongs to the campaign stream, not this aggregate.

## Later-stage follow-ups (not aggregate gaps)

- Catalog says an abort after a terminal outcome is a no-op for `CancelAccessReviewDecisionBatch`; the aggregate returns Conflict, so `CancelAccessReviewDecisionBatchHandler` must resolve that case. Verify when handlers are reviewed.
- Reactors/projectors for item reservations, fences and settlement stay with their stages.

## Evidence

- Jev: not requested; decisions follow the catalog and existing handler contracts.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessReviewDecisionBatchTests"`, then the unit suite.
- RED: none captured; not claimed.

## Review before build (independent reviewer, 2026-10-29)

Nothing was built or run.

### Findings and fixes

- Source: the added guards (owner stream on Publish, AcceptCancellation, CompleteCancellation; root equals batch id on Materialize) are correct and run before any `RaiseEvent`. No payload field duplicates stream identity or metadata. No gap found where a stale, duplicate or replayed input could append a second fact; exact-retry acknowledgment sits in `NativeAcceptance`, not the aggregate.
- Tests: every test asserts events appended or not, revision, or replayed state. Fixed compile risk: three `Assert.Equal(1, ...PendingEvents.Count)` calls would trip xUnit2013 (use `Assert.Single`); replaced with `Assert.Single(...)`.
- Not confirmed without a compiler: `NativeFacts.Ref()` field validity (shared helper), `AcceptedOperationBinding` namespace (`Bdgrz.Compliance.Features.NativeOperations.Models`, imported), `with { RootOperationId = ... }` on the provenance record (a positional record, so valid).

### Jev (model jev-1.14.0; request and response in `implementation-jev/accessreviewdecisionbatch-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
| --- | --- | --- |
| Where does a post-terminal abort become a no-op (catalog "none_outstanding" guard vs aggregate Conflict) | handler or process precondition | 0.85 (aggregate success no-op 0.11) |
| Aggregate enforces the invariant | true | 0.79 |
| Tests assert the invariant | true | 0.89 |
| Foreign-stream Validation guard is consistent | true | 0.78 |

Discrepancy disposition: catalog A005 expresses the terminal no-op as a process binding precondition, so the aggregate keeps Conflict. Decisive (0.85); no catalog or event change needed. The handler check stays a later-stage follow-up.

### Remaining risks

- Compile and run pending; blind-written tests.
- Competing appends rely on framework expected-position checks.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
