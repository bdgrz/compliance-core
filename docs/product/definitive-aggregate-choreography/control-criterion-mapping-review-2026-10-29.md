# ControlCriterionMapping aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Catalog entry verified: `aggregates.json` owner `ControlCriterionMapping` (selected split target, declared identity `(programId, controlId, editionId, criterionId)`), backlog group 11.

## Boundary decision

**Keep together.** One stream per mapping tuple. Identity is `AggregateIdentity.Create("ControlCriterionMapping", tenant, program, control, edition, criterion)`, so no caller key (mapping id, command id) forks a stream; the mapping id is only the logical operation identity of a proposal inside the stream, and tenant is the stream realm.

Named invariant: for one tuple there is at most one pending proposal and at most one accepted (active) mapping; an independent reviewer (or an exact waiver) approves exactly the pending proposal, an accepted successor supersedes only its predecessor, and retirement names the exact accepted proposal. A mapping references the criterion only by edition id and criterion identifier, never by criterion text.

Exclusions: criterion not-applicable decisions belong to `CriterionApplicability`; control approval and version resolution belong to the control owner; the program's selected edition belongs to the program; coverage, work items and directories are projections. The cross-owner checks (edition selected, control version approved, catalog entry) are admission checks in the handler before the fact is raised.

History bound: state is the compact `MappingLineHeader` plus current content reference; history is verified by the explicit reader (`MappingLineNativeHistory`). `ControlMappingNativeBoundaryTests` already proves no retained collection grows with 105 versions and a bounded native payload at maximum text length.

## Lifecycle

| State | Command | Result |
|---|---|---|
| no pending | Propose (new mapping id, expected revision matches) | `ControlCriterionMappingProposed`, pending |
| pending | Propose by anyone | conflict, no fact |
| pending | Review accept/reject by independent member (or exact waiver) | `ControlCriterionMappingReviewed`; accept makes it active, supersedes predecessor |
| pending | Review by proposer without exact waiver | forbidden, no fact |
| no pending | Review | not found / conflict, no fact |
| active | Propose with same content as active | conflict, no fact |
| active | Retire naming the active mapping at current revision | `ControlCriterionMappingRetired`, line has no active |
| superseded or unknown | Retire / Review | conflict or not found, no fact |
| any | same logical id, same authored intent | original result replayed (native acceptance), no fact |
| any | same logical id, changed intent | conflict, no fact |
| any | stale expected revision | conflict, no fact |

## Changes

No production change. Qualification found no behavioral defect. The new coverage addresses the one criterion without a direct test: the aggregate's own `Apply(MappingLinePrepared, ...)` append cut (stale revision or already-appended prepared fact is refused with no fact), and tuple identity stability.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks the stream | `ShouldDeriveStableIdentityFromTheTupleGivenSameAndDifferentTuples` (new); `ShouldKeepTuplesIndependentGivenDifferentCriteriaAndControls` (existing) |
| Named invariant: one pending, one active | `ShouldRejectSecondProposalGivenPendingOrStaleRevisionOrSameAcceptedContent`, `ShouldAdmitExactlyOneProposalGivenTwoCompetingProposalsOnOneTuple` (existing) |
| Independent review, exact waiver | `ShouldAllowProposerReviewGivenApprovedWaiverForExactRevision`, `ShouldRejectSelfReviewWithoutWaiverAndInvalidReviewGivenUnknownOutcomeStaleRevisionOrOtherProposal`, `ShouldRequireExactWaiverGivenProposerReviewingTheirOwnMapping` (existing) |
| Supersede only exact predecessor, retire exact accepted | `ShouldSupersedeOnlyTheExactAcceptedMappingAndRefuseOldRetirementGivenSuccessor`, `ShouldRefuseOldRetirementAndReviewGivenNewerAcceptedSuccessor` (existing) |
| Invalid decision appends nothing | `ShouldRefuseReviewAndRetirementGivenUnknownProposalOrStaleRevision` (existing); `ShouldRefuseSecondApplyAndKeepRevisionGivenPreparedFactAlreadyAppended`, `ShouldRefuseAppendAndKeepRevisionGivenFactPreparedAgainstALaterLineRevision` (new) |
| Exact retry, including after later facts | `ShouldReturnOriginalRegistrationAndConflictOnChangedIntentGivenProposalIdReuse`, `ShouldReturnOriginalReviewAndRetirementGivenExactRetriesAfterLaterChanges` (existing) |
| Changed intent refused | same two existing tests |
| Valid replay | `ShouldReplayTheSameLineGivenRoundTrippedFacts`, `ShouldProjectTheSameViewGivenReducerOverRaisedFacts` (existing) |
| Stale revision / competing decisions | protocol tests above; new stale-cut tests |
| Bounded history | `ShouldRetainOnlyCurrentAuthorityGivenManyMappingAndApplicabilityReviews`, `ShouldBoundNativeFactGivenMaximumValidEscapedMappingTexts` (existing) |
| Criterion referenced by id, not text | handler tests `ShouldRejectProposalGivenEditionNotSelectedByProgram` and catalog lookup in the propose handler (existing; the catalog rejects unknown criteria) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event changes needed. `ControlCriterionMappingProposed`, `ControlCriterionMappingReviewed` and `ControlCriterionMappingRetired` match the catalog's baseline events; `preliminaryNewEvents` is empty. Payloads carry the tuple, mapping/decision ids, version number, actor and operation provenance plus immutable content and first-response references; no criterion text is carried.

## Later-stage follow-ups (not aggregate gaps)

- Projector/reader consistency for coverage and work-item directories.
- Reactor for remap-required flagging when a successor control version is approved (already handler/projection level).
- The `ControlCriterionMappingRules` in-memory rules class is the line decision engine the aggregate composes; no change proposed.

## Evidence

Tests are written, not executed; build and run pending. Prior focused Jev review (2026-10-27, jev-1.14.0) put the probability that this is an independent tuple owner at 0.94; no new Jev request was made for this slice. Test helpers used (`MappingNativeSeeds.Rebind`, `MappingLineContent.PrepareJsonAsync`, `NativeContent.FreezeAsync`, `FirstResponses.FreezeAsync`) were read but not compiled; the new test file reuses the using set of existing mapping tests.

## Review before build (independent reviewer, 2026-10-29)

Nothing was compiled or run.

Findings: no defect in the implementation or the three new tests. Checked by reading:
- `Apply(MappingLinePrepared, ...)` refuses a stale or already-appended prepared fact with `Conflict` and raises nothing. The stale-revision error maps to `RequestErrorKind.Conflict`.
- Both append tests use only helpers that already exist (`MappingNativeSeeds.Rebind`, `MappingLineContent.PrepareJsonAsync`, `NativeContent.FreezeAsync`, `FirstResponses.FreezeAsync`). They match the call shapes in `MappingNativeSeeds.BuildAsync`.
- The `Propose` and `Review` argument orders in the test fixture match `ControlCriterionMappingRules`.
- Test names and Arrange/Act/Assert phases follow CNTRYL0001 and CNTRYL0002. Fixture ids are valid.
- The stale-cut test's final assertions (the originating owner still accepts the fact, revision 2) rule out a vacuous pass.

Fixes: none needed.

Jev (jev-1.14.0, request and response in `implementation-jev/controlcriterionmapping-test-impl-review-2026-10-29-*.json`; the first attempt failed schema validation with 422 and was re-sent corrected, no judgment recorded from it):
- Second-apply test asserts the invariant: 0.90.
- Stale-cut test asserts the invariant: 0.94.
- Discrepancy: tuple fields repeated in event payloads while the stream identity is a tuple hash. Choice keep_as_cataloged 0.76, remove_from_payload 0.21, needs_source_resolution 0.03 (confidence 0.64). Premise (fields cataloged and reducer-verified against the identity) 0.89. Disposition: keep as cataloged, no event change. The lead is moderate, and the payload is catalog-owned, so any removal would be a catalog decision.

Remaining risks:
- Not compiled. The test file carries a large `using` list copied from sibling tests. Strict unused-using analyzers could object, although sibling files carry the same style.
- The replay-digest mismatch at the same revision is covered only at handler level.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
