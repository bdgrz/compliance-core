# RiskTreatmentAction aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together.** One treatment action (`RiskTreatmentAction`, stream `risk-treatment-actions/{identity}`) decides atomically its work, the completions submitted for it and the independent reviews of those submissions.

- Identity: `AggregateIdentity.Create("RiskTreatmentAction", tenant, program, risk, action)`. The stream address derives from that only; the caller-supplied `ActionId` is the creation identity (catalog: exact retry with same id and intent returns the original result, changed intent is refused, a different id is a different record). Risk and program are part of the identity, so an action cannot move between risks and no other key forks a stream. A request id (`CommandId`) is operation provenance, not identity.
- Invariant (catalog): one accountable action, completion and independent review lifecycle. Open work is revised or canceled only when unfinished; a completion is submitted for open work; a review is accepted for the pending submission of the current generation and ends only by its decision or its fenced cancellation; a rejected completion reopens the work.
- Exclusions: assessment belongs to RiskEvaluation; risk governance consumes exact risk/source proofs (no live aggregate authority); evidence-request holds/releases belong to EvidenceRequest; reviewer independence and authorization, source-use proofs and the review reactor are handler/reactor stages.
- Alternatives: split reviews into their own aggregate rejected, because acceptance must seal the exact pending submission and block cancel/revise atomically. History is bounded by the action's own lifecycle (existing test covers 73 revisions with bounded aggregate state).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Add (own root, revision 0, logical id = action id) | open | `RiskTreatmentActionAdded` |
| added | Add again | conflict, no event | none |
| none | Revise / Cancel / Submit | missing-record refusal, no event | none |
| open | Revise (expected revision) | open | `RiskTreatmentActionRevised` |
| open | Submit | submitted | `RiskTreatmentActionCompletionSubmitted` |
| open / submitted | Cancel (no pending review) | canceled (terminal) | `RiskTreatmentActionCancelled` |
| submitted | AcceptReview | review pending, submission sealed | `RiskTreatmentActionCompletionReviewAccepted` |
| review pending | Complete (accept) | completed | `RiskTreatmentActionCompletionReviewed` |
| review pending | Complete (reject) | open again | `RiskTreatmentActionCompletionReviewed` |
| review pending | Abort | submitted, review fenced | `RiskTreatmentActionCompletionReviewCanceled` |
| review pending | Cancel / second AcceptReview | retryable conflict, no event | none |
| submitted | Revise / second Submit | conflict, no event | none |
| canceled / completed | any write | conflict, no event | none |
| any | stale expected revision | stale-version refusal, no event | none |
| no pending review | Complete / Abort review | conflict, no event | none |

Exact retry of a committed operation (including after later facts) and changed-intent refusal are decided by the shared `NativeAcceptance` (selected logical id) and the immutable operation provenance; existing integration tests cover replay of a selected historical review after 24 later rejected submissions and replay of a completed receiver after a later submission.

## Changes

- No source change. Reading the reducer against the catalog found no real gap: every Apply re-validates tenant, program, risk, action, next revision, status, own-root provenance and actor/time binding, so replay reproduces the live guards.
- Coverage added in the new aggregate-only file `RiskTreatmentActionAggregateTests.cs`: identity derivation, second add, foreign-operation add, writes before add, stale revision, revise/submit while submitted, cancel/second review while a review is pending, review decision/abort with no pending review, writes after cancellation, missing expected revision, valid-history replay then stale write.
- Existing four test files left unchanged.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveDistinctIdentityGivenDifferentRiskOrAction` |
| Add once, creation identity | `ShouldRefuseSecondAddGivenAddedActionIdentity`, `ShouldAppendNothingGivenAddByAnotherOperationIdentity` |
| Refusal appends no fact | `ShouldAppendNothingGivenReviseOrSubmitBeforeAdd`, `ShouldRefuseWriteGivenNonRootOrMissingExpectedRevision` |
| Stale revision | `ShouldRefuseStaleRevisionGivenWriteAtOlderRevision` |
| Owned lifecycle | `ShouldRefuseReviseAndSecondSubmitGivenSubmittedCompletion`, `ShouldRefuseEveryWriteGivenCancelledAction` |
| Pending review fences cancel and competing review | `ShouldFenceCancellationAndReviewGivenPendingReview`, `ShouldRefuseReviewDecisionGivenNoPendingReview` |
| Valid replay then guards | `ShouldReplayLifecycleGivenValidHistoryAndRefuseSameStaleWrite` |
| Historical review recovery, retry after later facts, bounded state | `ShouldRecoverSelectedHistoricalReviewGivenManyLaterRejectedSubmissions`, `ShouldReplayCompletedReceiverBeforeReleasedEvidenceGivenLaterSubmission` (existing) |
| Cancellation fence and proofs | `RiskTreatmentActionCancellationProofTests`, `RiskTreatmentActionUnboundCancellationTests` (existing) |
| Bounded command authority, forged submitter | `RiskTreatmentActionCompactionTests` (existing) |

Malformed-history tests were not added, by user preference. The review decision and abort happy paths need child operations bound to the accepted fact's committed metadata; they stay covered by the existing integration-style tests rather than unit-level provenance forgery.

## Event contract status

- Seven facts replay: Added, Revised, Cancelled, CompletionSubmitted, CompletionReviewAccepted, CompletionReviewed, CompletionReviewCanceled. No add, rename, remove or payload change is requested.
- Observation, not a request: the catalog `baselineEvents` lists five; the review accepted/canceled facts are the native split additions. Operation provenance in payloads belongs to the shared native-operation envelope and is left as is.

## Later-stage follow-ups (not aggregate gaps)

- Review reactor/handlers: reviewer independence, evidence-request hold/release proofs, source-use admission, abort wiring.
- `RiskGovernanceReader` action projection and HTTP transport (existing tests cover current behavior).
- Catalog `implementationStatus` still reads "SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED"; tracker row is the coordinator's to update.

## Evidence

- Jev: no request this round; the boundary rests on the catalog and code.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RiskTreatmentAction"`, then the unit suite.
- RED: none captured; not claimed (no behavior defect found).
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review; written, not executed (no build or test run).

Findings and fixes (tests only; no source change):
- Weak assertions: stale, state-conflict, cancelled and before-add tests asserted only `IsSuccess == false`, which a Validation refusal would also satisfy. They now assert the exact `RequestErrorKind` (Conflict for stale/state/pending, NotFound before add) and, where useful, status after refusal.
- Missing criterion: cancel of submitted work with no pending review (the positive side of the pending-review fence) had no test. Added `ShouldCancelSubmittedWorkGivenNoPendingReview`.
- Replay test now also asserts the replayed status after the stale refusal.
- Implementation: reducer re-validates identity, revision, status, own-root provenance and actor/time on replay; refusals return before `RaiseEvent`; no payload duplication found. No source gap.

Compile review: usings cover Risks.Aggregates/Commands/Services, NativeOperations.Models, AccessControl.Models, Tests.Testing, Cntryl.Portia(.Testing); `PendingEvents.Count` property used; `NativeFacts.Raised` used for history; `Assert.Equal(int, long)` infers long. Unconfirmed: that `Result.Error` is the property name (matches LocationAggregateTests usage) and implicit `Xunit` using.

Jev (jev-1.14.0, advisory), files `implementation-jev/risktreatmentaction-test-impl-review-2026-10-29-{request,response}.json`:
- Pending-review test proves fencing with no fact appended: noul 0.94.
- Before-add test proves nothing appended: noul 0.91.
- Catalog discrepancy: no_discrepancy 0.89 (add_to_model 0.06, remove 0.02, needs_source_resolution 0.03). Decisive; no event change requested.

Remaining risks: review decision/abort happy paths stay covered only by existing integration-style tests.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
