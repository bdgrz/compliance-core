# ApplicationImportBatch aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together** (catalog `boundaryDecision: selected_split`, group 47; split from the legacy `ApplicationImportLedger`, which is a cutover predecessor only). One batch stream (`Tenant / application_imports / BatchId`) decides, atomically, the batch's frozen source rows, its row outcomes and its single terminal disposition, at one logical revision shared by staging and execution.

- Identity: tenant, validated source key and namespace, and `BatchId`, which is the unchanged deterministic `BatchIdFor(StageApplicationImport)` (tenant, source key, namespace, `SubmissionId`). The stream address derives from tenant and batch id only, so no caller key forks a stream. Staging uses `SubmissionId` as its creation logical identity; every later command uses `CommandId` plus the expected revision.
- Invariant: frozen source rows, row outcomes, per-batch execution and terminal disposition. A batch stages once, is correlated only while open, seals one plan once, authorizes execution once for that plan, and ends with exactly one of committed, canceled or failed. A cancellation acceptance forbids any later publication.
- Exclusions: the source-level claim (`ApplicationImportSource`), per-target effects (`DeclaredApplication`), the reactors that drive them, the immutable row, plan and proposal content, and the projections of batch state are other owners or read models. The batch never consults the source's latest-batch pointer; its own `ApplicationImportCommitted` is the publication gate.
- Reactor/split: the source claim, target reservation and application effects are reactor coordination across owners and stay outside this aggregate. Rows and plans are externalised as verified immutable content references, so the authority holds no unbounded collections.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Stage, valid | revision 1, `preview_ready` | `ApplicationImportStaged` |
| none | Stage, row count under 1 or invalid count outside 0..rows | validation refusal, no event (new guard) | none |
| none | Stage under another submission or stream | validation refusal, no event | none |
| staged | Stage again | conflict, no event | none |
| staged, no plan | Correlate, expected = revision | revision + 1 | `ApplicationImportRowCorrelated` |
| staged, no plan | Correlate or proposal or accept, stale revision | conflict, no event | none |
| staged, no plan | Seal proposal (no current proposal) | revision + 1 | `ApplicationImportRetirementProposalSealed` |
| staged, current proposal | Seal proposal again | conflict, no event | none |
| staged, no plan | Seal plan, rows and target groups >= 1 | revision + 1, `accepting` | `ApplicationImportPlanSealed` |
| staged, no plan | Seal plan with zero rows or target groups | validation refusal, no event (new guard) | none |
| plan sealed | Correlate, proposal, accept | conflict, no event | none |
| plan sealed | Authorize execution, intent = sealed plan | revision + 1 | `ApplicationImportExecutionAuthorized` |
| plan sealed | Authorize again or different intent | conflict, no event | none |
| authorized | Commit, intent = sealed plan | revision + 1, `committed` | `ApplicationImportCommitted` |
| authorized | Commit with another intent | validation refusal, no event (new guard) | none |
| not authorized or cancel requested or ended | Commit | conflict, no event | none |
| staged, not committed or failed | Accept cancellation | revision + 1, `canceling` | `ApplicationImportCancellationAccepted` |
| committed, failed or cancel requested | Accept cancellation | conflict, no event | none |
| canceling | Complete cancellation | revision + 1, `canceled` | `ApplicationImportCanceled` |
| no accepted cancellation or ended | Complete cancellation | conflict, no event | none |
| sealed, not ended | (`ApplicationImportFailed` is applied on replay) | no command raises it; see open item | `ApplicationImportFailed` |

Committed, canceled and failed are terminal.

## Changes

Adversarial qualification found three real gaps where an invalid decision reached `RaiseEvent`, so the replay guard in `ApplyFact` threw `InvalidOperationException` mid-command instead of refusing with no fact.

- `Stage`: row count under 1, or invalid count below 0 or above the row count, now returns `Validation` before appending.
- `SealPlan`: row count or target group count under 1 now returns `Validation` before appending (after the stale-revision and open-state checks, so those keep priority).
- `Commit`: an intent that is not the sealed plan's frozen plan now returns `Validation` before appending.

No other production change. Verified as already sound: exact retry of a committed fact after later facts is decided by `NativeAcceptance.Decide` over the hydrated selected logical id, not current content; changed intent under a used id is `Conflict`; every append requires the exact verified native cut (`ApplicationImportBatchNativeCut`); the aggregate retains no collection, so history is bounded.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, exact retry returns original, changed content refused | `ShouldReplayOriginalRegistrationGivenIdenticalSubmissionAfterLaterChange`, `ShouldConflictGivenSameSubmissionWithChangedContent`, `ShouldKeepBatchIdentityAndDigestGivenCultureChange` (existing) |
| Stage at revision 1 | `ShouldStageAtRevisionOneGivenFreshBatch` (new) |
| Invalid staging appends nothing | `ShouldAppendNothingGivenInvalidRowCounts`, `ShouldAppendNothingGivenStagingRootedUnderAnotherSubmission` (new); `ShouldRejectWithoutEventGivenInvalidObservationShape` (existing, handler) |
| Second staging refused | `ShouldAppendNothingGivenSecondStagingOfStagedBatch` (new) |
| Valid replay, exact retry and changed intent | `ShouldRestoreStateAndDecideRetriesGivenValidStagedHistory` (new) |
| Exact correlation retry after later facts, changed intent | `ShouldReturnOriginalResultGivenExactCorrelationRetryAfterLaterChange` (existing) |
| Invalid correlation refused, stale revision | `ShouldRejectChoiceGivenInvalidBatchRowOrDecision` (existing) |
| Competing decisions at one expected revision | `ShouldRefuseSecondCorrelationGivenCompetingDecisionsAtSameExpectedRevision` (new) |
| Correlation refused after plan seal or cancellation | `ShouldRejectCorrelationGivenFrozenPlanOrCancellation` (existing) |
| Plan seal and no-op guard counts | `ShouldFreezeEveryRowGivenExplicitNewTargetChoices` (existing); `ShouldAppendNothingGivenPlanWithoutRowsOrTargetGroups` (new) |
| Plan refused when unready or stale | `ShouldRejectWholePlanGivenUnreadyOrStaleBatch` (existing) |
| Proposal sealed, invalidated by later change, refused when stale | `ShouldFreezeProposalGivenCompleteSourceOmissionAndInvalidateItOnLaterChange`, `ShouldRejectProposalGivenChangedDigestPartialCoverageOrSourcePosition` (existing) |
| Publication intent equals sealed plan | `ShouldAppendNothingGivenPublicationOfAnotherIntentThanTheSealedPlan` (new) |
| Publication and cancellation end-to-end, terminal result frozen | `ShouldFreezeOriginalDeclaredUnitEnvelopeGivenCommittedOrCanceledTerminalFact` (existing) |
| Bounded history, no retained collections | `ShouldKeepBoundedBatchAuthorityGivenManyCorrectionsOfOneStagedRow`, `ShouldKeepCompleteExternalChoicesGivenManyDistinctRowsWithoutRetainingThemInAuthority` (existing) |
| Intermediate history substitution detected | `ShouldRejectIntermediateSubstitutionGivenSameFinalBatchVersionAndHead` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

- Nine batch events: staged, row correlated, retirement proposal sealed, plan sealed, execution authorized, cancellation accepted, committed, canceled, failed. Payloads carry business facts plus native operation provenance and immutable content references; no metadata is duplicated.
- No event add, rename, remove or payload change is requested.
- Open item: `ApplicationImportFailed` is in the catalog and applied on replay, but the selected command contracts name no command that raises it (reactor failures are permanent conflict or quarantine). No command is invented; the producer is a later-stage decision.

## Later-stage follow-ups (not aggregate gaps)

- Handlers/reactors: confirm `CommitApplicationImportBatchHandler` passes the sealed plan's intent (the aggregate now refuses otherwise, rather than throwing), and decide which reactor or handler raises `ApplicationImportFailed`.
- Source claim, target reservation and application effects stay reactor-driven across `ApplicationImportSource` and `DeclaredApplication`.
- Batch listing, preview and state readers remain projections.

## Evidence

- Jev: no new request this round.
- Tests: written, not executed; build and run pending. Intended command: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ApplicationImportBatch"`, then the backend unit suite.
- RED: none captured. The new guard tests fail on the previous code (the append threw `InvalidOperationException` instead of returning a result), but that run was not executed here, so it is not claimed.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review of the source diff and `ApplicationImportBatchOwnerTests.cs`, read as the compiler and analyzers would. Nothing was built or run.

### Findings

- No correctness gap found. Each of the three new guards (Stage counts, SealPlan counts, Commit intent) refuses with `Validation` before `Append`, so no fact is raised and `ApplyFact` is never reached with the bad value. The guards mirror the checks already in `ApplyFact`, so a replayed history and a command decision agree.
- Guard order is correct: SealPlan and Commit evaluate stale-revision, lifecycle and cut conditions first, so a stale or ended batch still reports `Conflict`; only an otherwise acceptable decision reaches the new `Validation` refusal. Stage checks the root-operation shape, then counts, before the cut.
- Retries: exact retries of committed facts are acknowledged by the operation layer through `Acceptance.Decide` (covered by the replay test: exact is `Replayed`, changed intent is `Conflict`); the aggregate itself refuses a repeated Stage, authorization, acceptance or commit with `Conflict` and appends nothing.
- No payload field repeats metadata or stream identity beyond what the existing contracts carry. No event change is requested.
- Tests: each asserts a behavioral outcome (error kind, pending events, revision, plan or commit state, replay decision). The competing-correlation test overlaps an existing stale-revision test but still asserts the concrete outcome (Conflict, revision stays 2). Names follow CNTRYL0001 and every test has ordered Arrange, Act, Assert comments (CNTRYL0002).
- Compile read-through: usings cover `Uuid`/`Result`/`RequestErrorKind` (Cntryl.Portia), `AggregateScenario` (Cntryl.Portia.Testing), `AcceptedOperationBinding` and `NativeAcceptanceDecision` (NativeOperations.Models), `NativeFacts` and `ApplicationImportHost` (Tests.Testing), the aggregate and event namespaces. `OperationProvenance` is a record, so `operation with { AuthoredIntentSha256 = ... }` compiles. `RowCount` is `int`, `PendingEvents` uses `Assert.Single`/`Assert.Empty`, nullable `Error!` is used after `Assert.False`, and `Revision` is `long` against int literals (generic inference resolves to long). No code changes were needed.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/applicationimportbatch-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
|---|---|---|
| Add a failure command to the aggregate now? (add_command_now / leave_open_for_later_stage / remove_event / needs_source_resolution) | leave_open_for_later_stage | 0.97 (add 0.02, remove 0.00, needs source 0.01) |
| Noul: ApplicationImportFailed is a catalogued durable business fact that stays applied on replay although no command raises it | true | 0.71 |
| Noul: refusing Commit with another intent protects the one-terminal-disposition-for-the-frozen-plan invariant | true | 0.88 |
| Noul: the tests assert behavioral outcomes, not structure | true | 0.95 |

Discrepancy disposition: `ApplicationImportFailed` is catalogued (decisions.json: durable_business_fact 0.96) but no command raises it. Decisive lead (0.97) to keep the event, add no command, and leave the producer as a later-stage handler or reactor decision. Applied: no code change. The 0.71 Noul on the premise is moderate, so the producer remains a flagged open item, not approved completion.

### Remaining risks

- Nothing compiled or executed; the host-based tests assume `BatchHistoryAsync` confirms the native cut before a direct aggregate call (it calls `NativeCut.Confirm`) and that Correlate then Accept at revisions 1 and 2 succeeds for the default partial-coverage fixture, as the existing tests do.
- No command raises `ApplicationImportFailed`; mark the aggregate partial until its producer is decided.
- Confirm `CommitApplicationImportBatchHandler` passes the sealed plan's intent (later stage).


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
