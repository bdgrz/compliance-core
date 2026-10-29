# CriterionApplicability aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Catalog entry verified: `aggregates.json` owner `CriterionApplicability` (selected split target from `CriterionApplicabilityLedger`, identity `(programId, editionId, criterionId)` per tenant), backlog group 67.

## Boundary decision

**Keep together (selected split already applied).** One stream per `(tenant, program, edition, criterion)`. Identity is `AggregateIdentity.Create("CriterionApplicability", tenant, program, edition, criterion)`; the `DecisionId` is only the logical operation identity of a proposal inside the stream, and command ids of reviews and withdrawals bind to the stream and expected revision. No caller key forks a stream.

Named invariant: one applicability decision lineage. Only an independent accepted review (or an exact separation-of-duties waiver) can make a criterion not applicable; a withdrawal names the exact accepted decision and restores coverage. At most one decision is pending, and a new proposal is refused while one is pending or accepted.

Exclusions: coverage, readiness and work items are projections; program edition selection and criterion catalog lookup are handler admission checks; waiver approval belongs to `SeparationOfDutiesWaiver`; the control-to-criterion mapping belongs to `ControlCriterionMapping`.

History bound: state is the compact `MappingLineHeader` plus the current content reference; history is verified by the explicit native reader. `ControlMappingNativeBoundaryTests` covers the bounded retained state and maximum-length native payload.

## Lifecycle

| State | Command | Result |
|---|---|---|
| none or rejected or withdrawn | Propose (new decision id, expected revision matches) | `CriterionNotApplicableProposed`, pending |
| pending | Propose | conflict, no fact |
| accepted (not applicable) | Propose | conflict ("already not applicable"), no fact |
| pending | Review accept/reject by independent member or exact waiver | `CriterionApplicabilityReviewed`; accept excludes the criterion |
| pending | Review by proposer without exact waiver | forbidden, no fact |
| accepted | Withdraw naming the accepted decision at current revision | `CriterionNotApplicableWithdrawn`, coverage restored |
| older decision | Review or Withdraw | conflict, no fact |
| any | same logical id, same authored intent | original result replayed (native acceptance), no fact |
| any | same logical id, changed intent | conflict, no fact |
| any | stale expected revision | conflict, no fact |

## Changes

No production change. Qualification found no behavioral defect. The one criterion without a direct test was the aggregate's own `Apply(MappingLinePrepared, ...)` append cut and tuple identity stability; both are now covered by the new `CriterionApplicabilityAggregateTests`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks the stream | `ShouldDeriveStableIdentityFromTheTupleGivenSameAndDifferentTuples` (new); `ShouldKeepCriteriaIndependentGivenDifferentIdentifiers` (existing) |
| Named invariant: one pending/accepted decision | `ShouldRejectProposalGivenPendingStaleOrAlreadyNotApplicable`, `ShouldRejectSecondActiveProposalGivenPendingOrAcceptedDecision` (existing) |
| Independent review, exact waiver | `ShouldRejectSelfReviewGivenProposerWithoutExactWaiver`, `ShouldRequireExactWaiverGivenProposerReviewingTheirOwnDecision` (existing) |
| Lifecycle exclude then restore | `ShouldExcludeCriterionGivenAcceptedReviewThenRestoreGivenWithdrawal` (existing) |
| Old decision cannot be reviewed or withdrawn | `ShouldRefuseOldReviewAndWithdrawalGivenAnotherDecisionIsPendingOrAccepted`, `ShouldRefuseOldReviewAndWithdrawalGivenAnotherDecisionOnTheLine` (existing) |
| Invalid decision appends nothing | above plus `ShouldRefuseSecondApplyAndKeepRevisionGivenPreparedFactAlreadyAppended`, `ShouldRefuseAppendAndKeepRevisionGivenFactPreparedAgainstALaterLineRevision` (new) |
| Exact retry (including after later facts), changed intent refused | `ShouldReturnOriginalRegistrationAndConflictOnChangedRationaleGivenSameDecisionId`; withdrawal retry in the old-decision protocol test (existing) |
| Valid replay | `ShouldReplayAndProjectTheSameViewGivenReducerOverRaisedFacts` (existing) |
| Stale revision / competing decisions | protocol tests above; `ShouldKeepCriteriaIndependentGivenTwoDecisionsAtTheSameExpectedRevision`; new stale-cut test |
| Bounded history | `ShouldRetainOnlyCurrentAuthorityGivenManyMappingAndApplicabilityReviews` (existing, shared mapping boundary test) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event changes needed. `CriterionNotApplicableProposed`, `CriterionApplicabilityReviewed` and `CriterionNotApplicableWithdrawn` match the catalog's baseline events; `preliminaryNewEvents` is empty.

## Later-stage follow-ups (not aggregate gaps)

- Coverage and readiness projectors consuming the exclusion and restoration.
- Handler-level admission (edition selected by program, criterion exists in catalog) remains in the propose handler.

## Evidence

Tests are written, not executed; build and run pending. No new Jev request was made for this slice; the new test mirrors the already-reviewed `ControlCriterionMappingAggregateTests` shape and reuses its helpers (`MappingNativeSeeds.Rebind`, `MappingLineContent.PrepareJsonAsync`, `NativeContent.FreezeAsync`/`FreezeUnitAsync`, `FirstResponses.FreezeAsync`), read but not compiled. The copied `using` list may draw unused-using analyzer complaints.

## Review before build

Independent review, 2026-10-29. Catalog entry, aggregate source, new `CriterionApplicabilityAggregateTests` and its helper dependencies (`MappingNativeSeeds.Rebind`, `MappingLineContent.PrepareJsonAsync`, `MappingRuleFacts.Root`, `MappingLineTransitions.Next`) were read; nothing was compiled or run.

Findings:
- Implementation: `Apply(MappingLinePrepared, ...)` refuses with Conflict and no event on stale before-revision, owner version, committed position or replay digest; duplicate/replayed prepared facts cannot append twice. Committed-retry acknowledgment belongs to the native operation layer, not this method. No payload field duplicates stream identity. No defect found.
- Tests: the three new tests assert outcomes (Conflict kind, revision unchanged, no content, later valid apply succeeds). Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Fixture values are valid: the Root actor display is overwritten by `Rebind`, proposer and reviewer are distinct members, expected revisions 0 and 1 match line cuts.
- Compile risk: helper signatures match the sibling `ControlCriterionMappingAggregateTests` pattern; the large copied using list is the same as that sibling (no IDE0005 in .editorconfig). Unconfirmed: runtime that the second `ahead.Apply` accepts the review prepared before the raise (relies on `Version`/`CommittedStreamPosition` not changing on raise, as in the sibling test).
- No fixes were needed; no discrepancies between code and catalog (no event added, renamed or removed).

Jev (model jev-1.14.0; files `implementation-jev/criterionapplicability-test-impl-review-2026-10-29-request.json` and `-response.json`):
- Second apply of the same prepared fact: conflict_no_event 1.00 (decisive; matches code).
- Tests assert behavior and can fail (Noul): 0.92.
- Identity cannot be forked by a caller key (Noul): 0.75 (leaning true; the test covers tuple differences, DecisionId is not part of identity).

Remaining risks: tests unexecuted; tracker row is a shared file and unedited.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
