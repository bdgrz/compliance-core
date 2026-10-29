# ArtifactDisposition aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep.** One disposition (`artifact-dispositions/{DispositionId}` under the tenant) decides atomically one approved disposal intent bound to exact source content and its execution attempt history.

- Identity: `Tenant / artifact-dispositions / DispositionId`; `CommandId` is the logical operation id, never a stream key.
- Invariant: only an approved exact source with elapsed retention and no active holds or reliance may start execution; one outstanding attempt; a disposed result is terminal and yields a tombstone (content hash, size, retention basis, approving decision).
- Exclusions: holds, reliance, retention basis and artifact state (read as an eligibility snapshot by the handler), storage deletion itself, the hold fence receiver (`FenceArtifactDispositionForHold`, PLANNED reactor path), batching of approvals across artifacts.
- Catalog note: `boundaryDecision` says "process" while classification is business_aggregate with the three baseline events; the aggregate is retained as is, attempts capped at 16.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Approve, eligible, Org Admin, expected 0 | approved, rev 1 | `ArtifactDispositionApproved` |
| approved or failed | RequestExecution, unchanged eligibility, expected = revision | executing | `ArtifactDispositionExecutionRequested` |
| executing | RecordExecutionResult failed with code | failed | `ArtifactDispositionResultRecorded` |
| executing | RecordExecutionResult disposed | disposed + tombstone (terminal) | `ArtifactDispositionResultRecorded` |
| any | blocked eligibility, changed source, second outstanding attempt, terminal, attempt limit | conflict | none |
| any | exact retry of an earlier committed fact, including after later facts | success | none |
| any | changed intent, stale revision | conflict | none |

## Changes (real gap)

- Retry of a committed fact was acknowledged only for the latest attempt state (`RecordExecutionResult` looked only at the last attempt; `RequestExecution` only at a still-requested last attempt). After a later fact (for example a new attempt requested after a failed result) an exact retry was refused as stale. Both now match any attempt in history by actor, timestamp and outcome or source position, requiring `expectedRevision` below the current revision for non-latest matches so a fresh command is never mistaken for a retry. Changed intent still conflicts. This matches `selectedReplayRule` and the existing `Approve` behavior.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Named invariant (elapsed retention, no hold or reliance) | `ShouldRequireElapsedRetentionAndNoHoldsOrRelianceGivenApproval` |
| Eligibility recheck at execution, invalid decisions append nothing | `ShouldRecheckCurrentHoldsAndSourceBeforeExecutionGivenApprovedDisposition` |
| Lifecycle, failed attempt then tombstone | `ShouldRetainFailedAttemptAndCreateTombstoneOnlyAfterSuccessfulDispositionGivenExecution` |
| Replay of valid history | `ShouldReplayApprovalAttemptsAndTombstoneGivenCompletedDisposition` |
| No-op, exact retry, changed intent, stale revision, non-admin actor | `ShouldAcknowledgeExactRetryAndRefuseChangedIntentGivenApprovedDisposition` |
| Retry after later facts (result, request) | `ShouldAcknowledgeRetryOfCommittedResultGivenLaterAttemptRequested`, `ShouldAcknowledgeRetryOfCommittedRequestGivenLaterResultRecorded` |
| One outstanding attempt, competing/stale results, terminal, invalid outcome | `ShouldRefuseSecondAttemptAndResultWithoutOutstandingAttemptGivenExecutionState` |
| Bounded history | attempt cap constant (16) enforced in `RequestExecution`; no dedicated test |

Malformed-history tests were not added, by user preference.

## Event contract status

`ArtifactDispositionApproved`, `ArtifactDispositionExecutionRequested` and `ArtifactDispositionResultRecorded` carry the full `ArtifactDispositionView` state plus actor member id; no add, rename, remove or payload change requested. Catalog says `eventAuthority` requires schema reconciliation (baseline events); not decided here.

## Later-stage follow-ups

- Handler: bind `AttemptNumber` and `ExecutionRequestEventId` from the catalog `RecordArtifactDispositionResult` contract to the matching attempt; the aggregate currently identifies attempts by actor and time.
- Reactor: `FenceArtifactDispositionForHold` receiver (hold placed while executing); executor worker.
- Projector: eligibility snapshot assembly; disposition and tombstone views.

## Evidence

Tests written, not executed; build and run pending. RED not observed. Status: done pending build.

## Review before build (independent reviewer, 2026-10-29)

Nothing was compiled or executed. Source, tests and diffs were read as the compiler and strict analyzers would.

### Findings and fixes

- Implementation: the retry-after-later-facts change is correct. Traced: result retry after a later request (expected 2 < revision 4) succeeds without an event; a changed failure code falls through to stale conflict. Request retry after a failed result succeeds; a changed request time conflicts. No gap found in the invariant (eligibility recheck, one outstanding attempt, terminal disposed, 16-attempt cap, tombstone only on disposed). Catalog `RecordArtifactDispositionResult` fields `AttemptNumber`/`ExecutionRequestEventId` stay a handler-stage binding.
- Test structure (fixed): `ShouldRefuseSecondAttemptAndResultWithoutOutstandingAttemptGivenExecutionState` ran an action in Arrange and an `Assert.True` inside Act. Rewritten so every command runs in Act and every outcome (including the successful request and disposed result) is asserted in Assert; also asserts a single attempt remains.
- Coverage gap (fixed): the 16-attempt cap had no test. Added `ShouldRefuseSeventeenthAttemptGivenSixteenFailedAttempts` (16 failed cycles, then conflict, revision and attempt count unchanged).
- Fixture values were traced against `ValidateEligibility`/`ValidActor` (policy, digests, retains-through = period end + 7y, position 12, system actor with `reactor:` prefix); all valid.
- Pre-existing risk, not changed: `ShouldReplayApprovalAttemptsAndTombstoneGivenCompletedDisposition` passes `PendingEvents.ToArray()` to `Given`; if `Given` re-attaches metadata it would throw. Prior waves used the same pattern, so it is likely fine, but unconfirmed.

### Jev (model jev-1.14.0; request and response under `implementation-jev/artifactdisposition-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Result retry after later facts: success with no event, changed intent conflicts | success_no_event_exact_changed_conflict | 1.00 |
| Request retry: success with no event, changed time conflicts | success_no_event_exact_changed_conflict | 0.99 |
| Discrepancy: catalog result command carries AttemptNumber/ExecutionRequestEventId, aggregate does not | defer_to_handler | 0.98 (add_to_aggregate 0.02) |
| Tests assert behavior and can fail | true | 0.94 |

Disposition: decisive answers applied; no aggregate change for the binding gap. Catalog `boundaryDecision` "process" versus business_aggregate and `eventAuthority` schema reconciliation were not sent to Jev (shared catalog; owner decision), left unresolved and unchanged.

### Remaining risks

- Not built or run. `Assert.Equal(int, long)` on `Revision` relies on implicit widening (used already by existing tests).
- Retry matching for non-latest attempts compares requester, time and source position only, not full eligibility; handler must supply a monotonic time per attempt.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
