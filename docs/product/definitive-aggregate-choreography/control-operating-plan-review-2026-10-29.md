# ControlOperatingPlan aggregate review (2026-10-29)

Backlog group 65. Verified against the current `aggregates.json` entry (polished 2026-10-29). Aggregate stage only.

## Boundary decision

- **Identity:** deterministic `(tenantId, programId, controlId)` stream (`ControlOccurrenceIdentity.PlanLineOf`, area `control-operating-plans`, realm = tenant). `PlanVersionId` is the logical id of each proposal operation on that stream; approvals bind their `CommandId` and expected revision. No caller key forks the stream.
- **Invariant:** one independently approved, non-overlapping plan version line for a control: at most one proposal pending, an approval names exactly that pending version, revision counts only this line's facts.
- **Exclusions:** the complete plan content (owner, backup, reviewer, cadence, effective date) is immutable content referenced by the event; separation-of-duties, holder activity, cadence validity and overlap against the control version are caller-side line rules (`ControlOperatingPlanLine`) rechecked against the revision here. D19-05 (annual review occurrences) belongs to ControlOccurrence.
- **Decision:** keep as one aggregate (selected_split from ControlOperationsLedger already done). No reactor.

## Lifecycle

| State | Command | Result |
|---|---|---|
| no pending | Propose (expected revision = current) | `ControlOperatingPlanProposed`, pending = plan version id |
| pending | Propose | Conflict, no event |
| pending | Approve named pending version (expected revision = current) | `ControlOperatingPlanApproved`, pending cleared |
| none pending / other version | Approve | Conflict, no event |
| any | stale revision, foreign-stream or unversioned operation | refusal, no event |
| any | operation id already accepted on the line | Conflict, no event |

## Changes

- Propose: refuses an empty plan version id with Validation (previously the fold threw after `RaiseEvent`).
- Propose and Approve: refuse an operation whose logical id is the one already accepted for this hydration (`Acceptance.SelectedFact`) with Conflict (previously the fold's `Acceptance.Observe` threw "accepted more than once").

## Test-to-criterion table (`ControlOperatingPlanAggregateTests`)

| Criterion | Test |
|---|---|
| Stable identity | ShouldAddressStreamByTenantProgramAndControlGivenAnyOpener |
| Command raises fact; second proposal refused | ShouldRaiseProposalGivenEmptyLineAndRefuseSecondWhilePending |
| Stale revision, no event | ShouldAppendNothingGivenStaleExpectedRevisionOnProposal, ...OnApproval |
| Invalid decision, no event | ShouldAppendNothingGivenProposalWithoutPlanVersionIdentity, ...ApprovalWithoutPendingPlanOrOfAnotherVersion |
| Operation provenance guards | ShouldRefuseGivenOperationNotRootedOnOwnStreamOrWithoutExpectedRevision |
| Lifecycle and bounded revision | ShouldClearPendingGivenApprovalAndAllowNextProposal |
| Replay of valid history, stale after replay | ShouldRestoreStateGivenValidHistoryAndRefuseStaleCommandAfterReplay |
| Exact retry vs changed intent after later facts | ShouldRecognizeExactRetryAndRefuseChangedIntentGivenCommittedFactAfterLaterFacts |
| Reused operation id | ShouldAppendNothingGivenAlreadyAcceptedPlanVersionIdentityOnNewProposalOrApproval |

Existing `ControlOperatingPlanTests`, `ControlOperatingNativeHistoryTests` and `ControlOperatingIdentifiedOperationTests` cover the handler, line rules and history paths.

## Event contract status

`ControlOperatingPlanProposed` and `ControlOperatingPlanApproved` match the catalog (discriminators `bdgrz.control.operating_plan.proposed/approved`). Payload carries business facts (ids, revision, content refs, approver, rationale, waiver) plus native operation provenance; identity fields repeat the stream key as in the baseline. No event change needed.

## Later-stage follow-ups

- Handler: line rules (separation of duties, holder activity, cadence, overlap) and reassignment manifest preparation stay in the handler stage.
- Catalog `path` for the aggregate still lists `Features/Operations/ControlOperatingPlan.cs`; source now lives in `Aggregates/` (tracker note).
- Catalog source discriminator versions in `aggregates.json` read 1 while the events declare version 2; confirm in the event-catalog stage.

## Evidence

Tests written, not executed; build and run pending. Source change written with its tests (RED not run). Nothing was built, formatted or committed. Jev not consulted (no event naming or boundary question).

## Review before build (independent reviewer, 2026-10-29)

Nothing was built, run, formatted or committed. Read source, diff and `ControlOperatingPlanAggregateTests` as a compiler and the strict analyzers would.

### Findings

- Implementation: every accepted Propose/Approve checks own-stream root and expected revision, empty plan version id, already-accepted logical id, then pending/stale guards before `RaiseEvent`; the folds reject anything the guards let through, so no wrong or duplicate fact is appended. Exact retries are resolved by `NativeAcceptance.Decide` in the handler framework before the method runs (the Approve handler selects by `CommandId`), so the new Conflict guard is a defensive path only. No defect found.
- Tests: each test asserts appended events (`Pending`), revision, pending id or replay outcome; none is structure-only. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Fixture ids are `Uuid.CreateVersion4()` or `Uuid.Empty` (intentional); `NativeFacts.Ref()` and `Root` inputs are valid. History is built on one seed owner and `Raised` called once; state asserted after replay is captured before the next command. Empty/"foreign" provenance cases do not throw in `RootOperation.Provenance` (LogicalId is non-empty in Root calls; the empty-id case is passed to `Propose` via `Root(owner, Uuid.Empty, ...)`, see risk below).
- No fixes were required; no source or test edits made in this review.

### Jev (model jev-1.14.0; request/response in `implementation-jev/controloperatingplan-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Reused logical id at the aggregate method: Conflict, no event | conflict_no_event | 1.00 (applied; already the code) |
| Event payload repeating tenant/program/control ids | keep_baseline_for_now 0.60 vs remove_from_payload 0.39 | confidence 0.40, near-even: unresolved, no event change requested |
| Premise: payload repeats stream key | true | 0.97 |
| Tests assert the invariant | true | 0.88 |

Discrepancy disposition: the payload/stream-key duplication stays flagged unresolved for the event-contract stage (catalog approves current payload). Catalog `path` and discriminator version 1 vs 2 remain tracker/event-catalog follow-ups.

### Remaining risks

- `RootOperation.Provenance` was read and does not validate the logical id, so the empty-id test reaches the aggregate (confirmed).
- Not compiled or executed.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
