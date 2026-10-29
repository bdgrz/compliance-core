# IdentityRecovery aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run pending.

## Boundary decision

**Keep as a single narrow owner (catalog `business`, selected owner extension).** One stream (`bdgrz/identity-recoveries/{challengeId}`) decides one recovery challenge: the immutable both-proof-verified candidate, acceptance of the swap, forward completion, and rejection of an unaccepted preparation.

- Identity: platform + `challengeId`, established by the admitting owner/reactor (`EmailAddress` claim), never by a caller request key. Mutations bind `CommandId` (the native logical id) through `NativeAcceptance` on the hydrated owner. A later challenge has a different stream and can never rewrite an earlier recovery.
- Invariant (catalog): immutable both-proof-verified candidate and preparation; complete protection proofs precede the accepted exact swap; a preparation can reject/fence; an accepted swap completes forward; account, old/replacement identities and actor/proof provenance are retained without bearer tokens.
- Kept together: the candidate intent ref and stage only. Protection of the two identity slots is owned by `UserIdentity`; the aggregate records acceptance only when the receiver presents the protection proofs.
- Exclusions: bearer-token handling (never in facts), email challenge slots (`EmailAddress`), identity protection/registration/revocation (`UserIdentity`), completion notice (`IdentityRecoveryNoticeProcess`), source-proof verification and cross-owner predicates (receiver handlers).
- Alternatives: split (no, one linear decision over one frozen candidate); reactor (no, the decision is owner-local; reactors only drive it).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize | Prepared, intent frozen, generation 1 | `IdentityRecoveryPrepared` |
| none | Accept / Complete / Reject | conflict | none |
| prepared | Materialize (same or changed candidate) | conflict | none |
| prepared | Accept with the frozen intent | Accepted | `IdentityRecoveryAccepted` |
| prepared | Reject with the frozen intent | Rejected, fenced | `IdentityRecoveryPreparationRejected` |
| prepared | Complete / Accept or Reject with a different intent | conflict | none |
| accepted | Complete with the frozen intent | Completed | `IdentityRecoveryCompleted` |
| accepted | Reject / Accept again | conflict (forward only) | none |
| rejected | Accept / Complete / Reject again | conflict | none |
| completed | any decision | conflict | none |
| any | exact retry of a committed `CommandId`, even after later facts | original result via `NativeAcceptance.Decide` (receiver support) | none |
| any | same `CommandId`, changed intent | `NativeAcceptanceDecision.Conflict` | none |

## Changes (real gaps)

No source change. Every decision refuses before `RaiseEvent`, replay re-checks stage, owner, challenge, generation and frozen intent (`Require`), and `Acceptance.Observe` rejects a doubled logical id. The gap was owner-level coverage: existing tests exercise the aggregate only through host, handler and process paths. New file `IdentityRecoveryOwnerTests` covers the lifecycle matrix directly.

## Test-to-criterion table

Existing: `IdentityRecoveryHandlerTests`, `IdentityRecoveryReceiverPreparationTests`, `IdentityRecoveryProcessTests`, `IdentityRecoveryTransportTests`, `IdentityRecoveryLifetimeTests`, `IdentityRecoveryAdmissionPreparationTests` (host-level retry, supersession, dependency failure, bounded history, cancellation) are retained unchanged.

| Criterion | Test |
|---|---|
| Stable identity, stream per challenge, revision 0 and generation | `ShouldFreezeCandidateAtChallengeStreamGivenFirstMaterialize`, `ShouldKeepStreamsDistinctGivenDifferentChallenges` |
| Refusal appends no fact | `ShouldRefuseAndAppendNothingGivenAnyDecisionBeforePreparation`, `ShouldRefuseAcceptRejectAndCompleteGivenDifferentIntentOrSkippedStage` |
| Candidate immutable (second materialize) | `ShouldRefuseSecondCandidateAndAppendNothingGivenFrozenPreparation` |
| Forward completion, consecutive revisions, no reject after accept | `ShouldCompleteForwardAtConsecutiveRevisionsGivenAcceptedSwap` |
| Terminal after completion | `ShouldRefuseEveryLaterDecisionGivenCompletedSwap` |
| Reject fences accept/complete, reject is not repeatable | `ShouldFenceAcceptAndCompleteGivenRejectedPreparation` |
| Replay/hydration of valid history | `ShouldHydrateEachStageGivenValidHistory`, `ShouldContinueRejectionGivenReplayedPreparation` |
| Exact retry after later facts, changed intent | `ShouldFindCommittedOperationAndConflictChangedIntentGivenReplayAfterLaterFacts`; host-level `ShouldReplayEqualWinnerGivenPreparationIntentBecomesUnavailableAfterActualAcceptance` |
| Competing/stale appends | revision is the hydrated version (asserted 0,1,2); the append is guarded by the framework executor and exercised through the host tests |
| Bounded history | compact owner (one intent ref and a stage); `ShouldKeepIdentityAuthorityBoundedAndFenceEarliestProtectionGivenManyRealRejectedRecoveries` |

Malformed-history tests were not added, by user preference.

## Event contract status

Four events (`IdentityRecoveryPrepared`, `IdentityRecoveryAccepted`, `IdentityRecoveryCompleted`, `IdentityRecoveryPreparationRejected`) match the catalog baseline events and carry owner, revision, generation, intent ref, proof refs, operation provenance and challenge id; no bearer token. The decision record types `IdentityRecoveryPreparationRejected` as a durable business fact (Jev 0.92). No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers/reactors: `RejectIdentityRecoveryPreparation` has the selected command contract but is driven by the process; its HTTP-free handler wiring stays as is.
- Catalog status `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` is a shared-file item.
- `FirstResponse` and `OperationResult` are always null on these facts; receivers return outcomes through `ReceiverOutcome`, so this is a handler-stage question, not invented here.

## Evidence

- Jev: not consulted; no open design decision.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~IdentityRecovery"`.
- RED: none captured; not claimed. The new tests cover existing behavior and are expected to pass on first run.
- Compile risks to watch: `EventStreamAddress.Resource` as the stream's third segment, `Assert.Equal(0, prepared.Revision)` long/int inference, and `NativeAcceptanceDecision` namespace (assumed `Bdgrz.Compliance.Features.NativeOperations.Models`).

## Review before build

Reviewer pass, written not executed (no build, test or format run). Source `IdentityRecovery.cs` has no uncommitted change this session.

### Findings and fixes

- Owner tests passed `null` proof refs to `Materialize`, `Accept` and `Complete`. Every production caller (the three receiver handlers, `IdentityRecoveryProcessTests` reject) passes a non-null ref, and the catalog says complete protection proofs precede the accepted swap, so those tests exercised a shape the invariant does not intend. Fixed: all calls now pass `NativeFacts.Ref()`, and the first test asserts `Prepared.SourceProofs` and `Intent` carry the supplied refs (an existing check already asserts Accepted/Completed/Rejected carry theirs).
- `Assert.Equal(0, prepared.Revision)` changed to `0L` to remove the long/int inference risk.
- No source defect found: each decision refuses before `RaiseEvent`, replay re-validates stage, owner, challenge, generation and frozen intent, `Acceptance.Observe` rejects a doubled logical id, no payload field repeats metadata, and exact retries are acknowledged by the receiver through `NativeAcceptance.Decide` with no new event.

### Jev (advisory, model jev-1.14.0; `implementation-jev/identityrecovery-test-impl-review-2026-10-29-*.json`)

Discrepancy: the owner methods accept a nullable protection/completion/conflict/source proof ref (`ImmutableContentRef?`), so an `IdentityRecoveryAccepted` with no protection proof is constructible, though the receiver always verifies and supplies one.

| Question | Answer |
|---|---|
| Refuse null in aggregate vs allow (receiver verifies) vs needs resolution | allow 0.70, refuse 0.29, needs resolution 0.01 (confidence 0.55) |
| Premise: accepted fact with no protection proof violates the rule | Noul 0.63 |
| Premise: null-proof tests faithfully check the invariant | Noul 0.22 (tests exercise a shape the invariant forbids) |

Disposition: choice leads only 0.70 against a 0.63 premise that null violates the rule, which is not a decisive, consistent answer. Unresolved; no source change made. The tests were corrected independent of the answer (Noul 0.22 agreed with using real refs). Open item for the owner: whether to tighten `Accept`/`Complete`/`Reject`/`Materialize` to non-null parameters (handlers already satisfy it; no event or schema change, since the event fields stay nullable).

### Remaining risks

- Compile unconfirmed: `owner.Stream.Resource` (used elsewhere in tests on `EventStreamAddress`), `AcceptedOperationBinding` `with` on a record, `Facts` via `AggregateScenario.PendingEvents.ToArray()`.
- Tests assert in Arrange via `Assert.True(...IsSuccess)` setup calls; confirm CNTRYL0002 accepts this as other suites do.
- Catalog status `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` remains a shared-file item.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
