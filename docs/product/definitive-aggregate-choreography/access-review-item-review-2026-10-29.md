# AccessReviewItem aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (selected split from `AccessReviewCampaign`, retained).** One frozen item (stream `access-review-items/{AggregateIdentity(tenant, campaign, item)}` under the tenant) decides atomically its single materialization, reviewer decision, reassignment, remediation change/verification/exception history, and its bulk-decision and campaign-completion source uses.

- Identity: tenant + campaign + item. The stream id derives from those only, so a mismatched pair is simply absent; no caller command key forks a stream.
- Invariant (catalog): one reviewer decision, reassignment, remediation verification, exception and history per frozen item; privileged items accept only an individual named decision. The logical revision counts business facts (materialization is revision 1); source-use and fence facts do not advance it.
- Exclusions: decision and remediation content rules (including the privileged-individual rule in `AccessReviewRules`, decided by the caller from item state), campaign completion orchestration, batch lifecycle and publication, population snapshots, authorization.
- Splitting decision from remediation would break one-owner revision ordering; the batch and campaign remain separate owners coordinated by source uses.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (own stream) | opened, revision 1 | `AccessReviewItemOpened` |
| none | Materialize (foreign stream) | validation refusal | none |
| none | any change, bulk hold, fence | retryable conflict | none |
| opened | Materialize | conflict | none |
| opened, no active use, open campaign | decision, change, verify, exception, reassign at current revision | next revision | respective fact |
| opened | same, stale or ahead revision, no expected revision, foreign operation | conflict / validation | none |
| opened | same, uses active (completion or bulk hold) | retryable conflict | none |
| opened | bulk hold grant, apply provisional, settle | source-use and provisional facts | `...DecisionAuthorized`, `AccessDecisionsRecorded` (batch), `...DecisionSettled` |
| opened | fence of selected batch (first time) | fenced | `AccessReviewItemDecisionCanceled` |
| fenced / published batch | fence again | conflict (new) | none |
| fenced batch | hold or apply | conflict | none |
| opened | completion use granted, settled `ParentClosed` | closed | completion-use facts |
| closed | any change or hold | conflict | none |

## Changes (real gaps)

- `FenceBulkDecision` did not refuse a batch already fenced or already published on the item; the reducer threw `InvalidOperationException` after the append was raised (an unhandled failure instead of a conflict). It now returns a `Conflict` result before anything is raised. Exact retry of a committed fence stays with the acceptance/selected-fact path (unchanged).
- No other code change. The aggregate had no direct owner-level test file; `AccessReviewItemTests.cs` was added.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveOneStreamGivenSameTenantCampaignAndItem` |
| Valid materialization, payload | `ShouldRaiseRevisionOneOpenedFactGivenNewItem` |
| Invalid decision appends nothing | `ShouldAppendNothingGivenMaterializationOnAnotherStream`, `ShouldAppendNothingGivenChangeBeforeMaterialization`, `ShouldAppendNothingGivenChangeWithoutExpectedRevisionOrOwnStream` |
| Changed intent / second materialization | `ShouldRefuseSecondMaterializationGivenOpenedItem` |
| Stale revision | `ShouldAcceptChangePreparationOnlyGivenCurrentRevisionOfReplayedItem` |
| Replay of valid history | `ShouldRestoreItemStateGivenValidOpenedDecisionAndChangeHistory` |
| Replay requires finalized cut (exact retry/competing append safety) | `ShouldAppendNothingGivenDecisionOnReplayedItemWithoutFinalizedCut` |
| Bulk hold guards | `ShouldRefuseBulkHoldGivenUnmaterializedOrForeignBatchItem` |
| Fence guards (unmaterialized, foreign root, repeat) | `ShouldRefuseFenceGivenUnmaterializedItemOrForeignBatchRoot`, `ShouldRefuseSecondFenceAndHoldGivenBatchAlreadyFencedOnItem` |

Malformed-history tests were not added, by user preference. Existing handler and process suites (`AccessRemediationTests`, `AccessReviewBulkDecisionTests`, `AccessReviewCompletionProcessTests`, `AccessReviewNativeBoundaryTests`) cover exact retry after later facts, closed-campaign refusal, bulk apply/publish and settlement through the fixture; they were not modified.

## Event contract status

- `AccessReviewItemOpened`, `AccessDecisionsRecorded`, `AccessRemediationChangeRecorded`, `AccessRemediationVerified`, `AccessRemediationExceptionRecorded`, `AccessReviewResponsibilityReassigned` carry business facts and content refs plus operation provenance; no metadata duplication found. Source-use facts (`...DecisionAuthorized/Settled/Canceled`, `...CampaignCompletionUseAuthorized/Settled`) are technical owner facts that do not advance the revision.
- No event add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Privileged-entitlement rule (individual named decision only; bulk or delegated refused) is enforced by the caller (`AccessReviewRules`) from item state; the aggregate holds opaque content refs. A handler/reactor test that the bulk apply path refuses privileged items belongs to the handler stage.
- Verification against a later accepted population snapshot ("ticket alone is refused") is handler/rule work.
- Direct owner-level tests of the closed-campaign state and bulk apply/provisional visibility need source-use history builders; currently covered through the fixture-level process tests.

## Evidence

- Jev: no request this round; the single change follows the catalog invariant and the existing guard pattern (`PrepareBulkHold`).
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessReviewItemTests"`, then the unit suite.
- RED: none captured; not claimed. Residual risk: compile of the new test file and replay of the hand-built `AccessReviewItemDecisionCanceled` fact are unverified.

## Review before build

Independent review, nothing built or run.

### Findings and fixes

- **Test that could not fail (fixed).** `ShouldRefuseSecondFenceAndHoldGivenBatchAlreadyFencedOnItem` passed even without the new `FenceBulkDecision` guard: the replayed owner has no confirmed replay cut, so `Append` returned Conflict first (and the committed fence's logical id would equal the selected one, the exact-retry path). Kept for the hold and retry refusal, but the guard itself is now proven by `ShouldRefuseFenceGivenBatchAlreadyFencedByAnotherOperationUnderConfirmedCut`: the cut is confirmed, the committed fence came from logical operation L1, the selected operation is L2 with the same root batch. Without the guard the reducer throws; with it the result is Conflict and nothing is pending.
- **Missing positive and competing-append coverage (fixed).** Every earlier test refused; none showed an accepted change. Added `ShouldAppendNextRevisionDecisionGivenConfirmedCutAndCurrentRevision` (revision 2 `AccessDecisionsRecorded`, no batch, no publication) and `ShouldRefuseSecondChangeGivenStaleExpectedRevisionAfterFirstUnderConfirmedCut` (competing second change at the old revision refused, one event only).
- Implementation re-read: the single source change is correct, returns Conflict before anything is raised and leaves exact retry of a committed fence on the acceptance path. No payload field duplicates stream identity or metadata (TenantId/CampaignId/ItemId are the item identity carried by existing contracts; not changed here).
- Compile read of the test file: usings match siblings, `ReplayCut` is internal with InternalsVisibleTo to the test assembly (sibling `AccessReviewCampaignAggregateTests` uses the same Confirm call), `with` on the provenance record, collection expression spread into `DomainEvent[]`, `.Count` never used, replayed facts built with `DomainEventSeed.Attach` on fresh events only. Not confirmed by a compiler.

### Jev (jev-1.14.0, advisory), `implementation-jev/accessreviewitem-test-impl-review-2026-10-29-*.json`

| Question | Answer |
|---|---|
| Fence repeat test proves duplicate fence refused | noul 0.89 |
| Stale-after-first test proves competing change refused | noul 0.89 |
| Catalog/code event discrepancy | no_discrepancy 0.89 (add_to_model 0.02, remove 0.02, needs_source_resolution 0.07) |

Decisive on all three; no event discrepancy, no catalog change requested.

### Remaining risks

- `AuthorizeCampaignCompletionUse` has no explicit closed-item guard: a completion grant by a new operation on an already closed item would reach the reducer and throw instead of returning Conflict. Likely unreachable (a closed item's campaign is complete) and needs source-use history builders to test first; left as a follow-up rather than adding untested code.
- Fence after the batch was published is guarded but not owner-tested (needs settled-source-use history).
- Privileged-individual rule and snapshot-based verification remain caller/handler stage.
- All tests are unexecuted.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
