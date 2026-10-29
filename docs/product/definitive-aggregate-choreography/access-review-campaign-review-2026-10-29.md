# AccessReviewCampaign aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and routes are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One campaign (`AccessReviewCampaign`, stream `access-review-campaigns/{campaignId}` under the tenant) decides atomically the frozen launch snapshot, the logical revision, and the single compact completion gate.

- Identity: tenant + caller-supplied immutable `CampaignId` (the launch's logical id equals the stream id). A request key cannot fork a second campaign stream; later operations are bound by CommandId and the expected revision.
- Invariant: one launch per campaign; at most one completion admitted at a time, which seals the campaign against a second completion or a bulk admission until it is closed or canceled; closure and cancellation act only on the exact pending completion root; completion is terminal.
- Exclusions: per-item decisions, remediation and reviewer state (`AccessReviewItem`), bulk batch execution (`AccessReviewDecisionBatch`), item materialization, completion proof verification, hold/settle of items (reactors), authorization and projections.
- Items and batches are already split into their own streams; the campaign keeps no roster. Item work never changes the campaign revision, so history is bounded to launch, completion admission, bulk admissions, closure and cancellation (bounded-history test exists in `AccessReviewNativeBoundaryTests`).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Launch (own campaign operation) | launched, revision 1 | `AccessReviewCampaignLaunched` |
| none | Launch (other logical id) | validation refusal | none |
| none | Complete / bulk | not found | none |
| launched | Launch again | conflict (handler resolves exact retry from the committed fact) | none |
| active | AcceptCompletion (current revision) | pending, revision +1 | `AccessReviewCampaignCompletionAccepted` |
| active | AcceptCompletion / bulk (stale revision) | conflict | none |
| active | AdmitBulkDecision (current revision) | revision +1 | `AccessReviewDecisionBatchAdmitted` |
| pending | second completion or bulk | conflict | none |
| pending | Close (exact pending root) | completed, revision +1 | `AccessReviewCampaignCompleted` |
| pending | Cancel (exact pending root) | active again, revision +1 | `AccessReviewCampaignCompletionCanceled` |
| none pending | Close / Cancel | conflict | none |
| completed | any completion, bulk, close, cancel | conflict (terminal) | none |
| any | retry of a committed operation | refused at the replay cut (`SelectedFact` set); the handler returns the original via `AcceptedFor` / `AdmittedFor` / `CanceledFor` / `ClosedFor` | none |

## Changes (real gaps)

No source change. Reading the source against the current catalog entry found no behavioral gap: identity is stable, every guard appends nothing on refusal, retry resolution works from the selected committed fact even after later facts, and replay reproduces the live guards.

The aggregate had no owner-level unit tests (only fixture-driven integration tests through handlers). Added `AccessReviewCampaignAggregateTests` (new file, aggregate-only) covering the criteria below. `AccessReviewCampaignTests` is unchanged.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Valid launch, trimmed payload, revision 1 | `ShouldLaunchAtRevisionOneGivenFreshCampaign` |
| Invalid launch appends nothing | `ShouldRefuseLaunchGivenOperationOfAnotherCampaign` |
| Duplicate launch, changed intent refused | `ShouldRefuseLaunchGivenCampaignAlreadyLaunched` |
| Replay of valid history to completed | `ShouldRestoreCompletedCampaignGivenValidLaunchAdmissionAndClosureHistory` |
| Completion admission and seal | `ShouldAdmitCompletionAndSealCampaignGivenCurrentRevision` |
| Stale revision (completion, bulk) | `ShouldRefuseCompletionGivenStaleExpectedRevision`, `ShouldRefuseBulkAdmissionGivenStaleExpectedRevision` |
| Refusal with no fact on unlaunched campaign | `ShouldRefuseCompletionGivenCampaignNeverLaunched`, `ShouldRefuseBulkAdmissionGivenCampaignNeverLaunched` |
| Competing completion / bulk during seal | `ShouldRefuseSecondCompletionAndBulkAdmissionGivenCompletionPending` |
| Terminal campaign | `ShouldRefuseCompletionAndBulkAdmissionGivenCompletedCampaign`, `ShouldRefuseCancellationGivenCampaignAlreadyCompleted` |
| Bulk admission advances revision | `ShouldAdmitBulkDecisionAndAdvanceRevisionGivenLaunchedCampaign` |
| Closure of exact pending completion | `ShouldCloseCampaignGivenExactPendingCompletion` |
| Closure/cancel with no or foreign pending root | `ShouldRefuseClosureGivenNoPendingCompletion`, `ShouldRefuseClosureAndCancellationGivenAnotherCompletionRoot` |
| Cancellation reopens admission | `ShouldCancelPendingCompletionAndReopenAdmissionGivenFailureProof` |
| Late close/cancel after cancellation | `ShouldRefuseCancellationAndClosureGivenCompletionAlreadyCanceled` |
| Exact retry after later facts (completion, bulk) | `ShouldReturnCommittedCompletionWithoutNewFactGivenExactRetryAfterCancellation`, `ShouldReturnCommittedBulkAdmissionWithoutNewFactGivenExactRetryAfterLaterAdmission` |
| Selection of an unrelated operation | `ShouldSelectNothingGivenHistoryHydratedForAnotherOperation` |
| Append requires finalized replay cut | `ShouldAppendNothingGivenCompletionOnReplayedCampaignWithoutFinalizedCut` |
| Bounded history | existing `ShouldKeepBoundedCampaignAuthorityGivenManyAdmittedIndependentBatches` |
| Competing appends / OCC | Portia expected-position append (framework); handler re-decides |

Malformed-history tests were not added, by user preference.

## Event contract status

- Events: `AccessReviewCampaignLaunched`, `AccessReviewCampaignCompletionAccepted`, `AccessReviewDecisionBatchAdmitted`, `AccessReviewCampaignCompleted`, `AccessReviewCampaignCompletionCanceled`. All are business facts; no add, rename, remove or payload change is requested. The catalog event list matches.
- Observation for the shared owner: source declares discriminator version 2 for `bdgrz.access_review.campaign.launched` and `bdgrz.access_review.campaign.completed`, while the catalog lists version 1. The catalog entry should record the shipped version; no source change.
- `AccessReviewCampaignCompleted` names its generation `CampaignGeneration` where siblings use `Generation`; a persisted payload name, so left alone.
- Catalog `baselineSourcePath` points at the old pre-layout path (`Features/AccessReviews/AccessReviewCampaign.cs`); the current path is `Features/AccessReviews/Aggregates/AccessReviewCampaign.cs`.

## Later-stage follow-ups (not aggregate gaps)

- Launch field validation (name, instructions, deadline) and exact launch-retry comparison belong to `LaunchAccessReviewCampaignHandler`.
- Closure proof verification, item hold/settle and generation fencing of delayed closures are reactor/handler work (`AccessReviewCompletionReactor`, settlement reactors).
- `AbortAccessReviewCampaignCompletion` abort-after-terminal no-op is handler behavior over `CanceledFor` / `ClosedFor`.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source behavior.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessReviewCampaignAggregateTests"`, then the unit suite.
- RED: none captured; not claimed. No source defect was found, so there is no production change to turn green.
- Residual risk: the tests use `ReplayCut.Confirm` (internal) to finalize the replay cut and attach fact positions equal to revision; both unverified until built. Handler suite not run.

## Review before build

Independent review before any build. Nothing was compiled or run.

### Findings and fixes (all in `AccessReviewCampaignAggregateTests`; no source change)

- Weak tests: `ShouldRefuseCompletionGivenCampaignNeverLaunched` and `ShouldRefuseBulkAdmissionGivenCampaignNeverLaunched` called only `Prepare*`, so "appends nothing" was vacuous. They now call `AcceptCompletion` / `AdmitBulkDecision` and assert no pending event and not launched.
- Weak test: `ShouldRefuseCompletionAndBulkAdmissionGivenCompletedCampaign` called only `Prepare*`. It now issues real `AcceptCompletion` / `AdmitBulkDecision` commands under a confirmed cut for a new logical id, and asserts revision 3 and no fact.
- Missing criterion: completion and bulk admission require an expected revision. Added `ShouldRefuseCompletionAndBulkAdmissionGivenOperationWithoutExpectedRevision` (Validation, no fact).
- Missing criterion: the generation fence against a delayed closure. Added `ShouldFenceDelayedClosureOfCanceledCompletionGivenNewCompletionPending` (launch, A accepted, A canceled, B accepted; closing root A is refused, closing root B appends one Completed fact at revision 5 with B's intent).
- Implementation: every `On` guard, the exact-root checks in `Close` / `CancelCompletion`, the seal in both `Prepare*` methods, and the cut requirement in `Append` enforce the invariant. A stale, duplicate, competing or replayed input appends nothing, and a retry of a committed operation is refused at the cut with the original recoverable through `AcceptedFor` / `AdmittedFor` / `CanceledFor` / `ClosedFor`. No defect found. Minor observation (not changed): `AcceptCompletion` does not itself check `RootOperationId == LogicalId`; the `On` handler for bulk does. Handlers always build root provenance, and `Close` / `CancelCompletion` compare root ids, so the gate is unaffected.
- Compile read: usings cover `AggregateScenario` (`Cntryl.Portia.Testing`), `DomainEventSeed`, `ImmutableContentRef`, `ActorReference`, `OperationProvenance`, `NativeFacts`. Event constructor argument order matches `Common/Features/AccessReviews/Events`. `OperationProvenance` has no constructor validation, so `Op` is safe. `ReplayCut` is internal with `InternalsVisibleTo` for the test assembly and the `Confirm` pattern is the one `AccessReviewItemTests` uses. Fact positions 1..n are consecutive in every history. Selected logical ids equal the operation logical ids at every append.
- Could not confirm without a build: CNTRYL/style analyzers on the new tests, `Assert.Equal(long, long?)` inference (expected to compile), and JSON round-trip of the launch fact (the same path other aggregate tests use).

### Jev (model jev-1.14.0, advisory; request/response in `implementation-jev/accessreviewcampaign-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Delayed-closure test proves the exact-root fence | true | 0.91 |
| Sealed test proves seal against competing completion and bulk | true | 0.95 |
| Retry test consistent with "acknowledge from committed fact, no new fact" | true | 0.95 |
| Payload identity (TenantId/CampaignId repeat stream identity on Launched and Completed): change now? | no_change_now | 0.62 (remove_fields 0.34, confidence 0.43) |
| Catalog version and shape | update_catalog_record_shipped_shape | 1.00 |

Dispositions:

- Discriminator version 1 (catalog) vs 2 (source) on Launched and Completed, and `ItemIntentManifest` present in source `AccessReviewCampaignLaunched` but absent from the catalog event fields: Jev decisive (1.00). Catalog owner should record the shipped shape. No source change; reported under remaining.
- Payload fields repeating stream identity (`TenantId`, `CampaignId` on Launched and Completed): **unresolved**. Jev leads (0.62) but with low confidence (0.43). Catalog and code agree and the events are shipped, so nothing is changed here; it is a shared catalog and schema decision.
- `CampaignGeneration` vs `Generation` naming: left alone (persisted payload name, unchanged from the earlier note).

### Remaining risks

- Tests are written, not executed. No RED was captured.
- The payload-identity question stays open for the catalog owner.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
