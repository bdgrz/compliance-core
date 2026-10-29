# ControlDraft aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Catalog entry verified: `aggregates.json` owner `ControlDraft` (retained baseline, polished 2026-10-29, `boundaryDecision: narrow`), backlog group 64, status "Source present; verify". Source: `src/Compliance.Core/Features/Controls/Aggregates/ControlDraft.cs`.

## Boundary decision

**Keep together, narrow.** One stream per control: `Tenant / controls / ControlId`, where `ControlId` is the deterministic UUIDv5 over tenant, program and normalized identifier (`ControlDraft.IdFor`). The reducer rejects any fact whose id differs from that derivation, whose program or identifier changes after creation, or whose tenant differs from the stream tenant. No caller key forks the stream; `CommandId` is operation identity, never stream identity.

Named invariant: only the exact current draft or retirement can be independently reviewed and then activated; an accepted successor or retirement changes one control version line without overlapping effective intervals; responsibilities cannot create unwaived conflicts with decisions already made on the exact revision.

The aggregate keeps only compact current authority (`ControlAuthorityHeader`, an immutable `CurrentAuthority` content reference and a bounded native sequence). Full bodies, approved version lists and decision histories are immutable content or read-side composition (catalog rationale). The 70-revision native history test shows the header does not grow with revisions.

Exclusions: authorization and program scoping belong to handlers; impact previews, evidence, mapping, work and risk follow-ups belong to reactors and projections; criterion mapping, evaluation plans and occurrences are separate owners; source-use reservation bookkeeping is owned by `SourceUseReservations` (it only fences writers).

## Lifecycle

| State | Command | Result |
|---|---|---|
| empty | CreateControlDraft | `ControlDraftCreated` (revision 1, open initial draft) |
| open draft | ReviseControlDraft | `ControlDraftRevised` (revision + 1); unchanged content is a native no-op receipt |
| open initial draft, never reviewed, no responsibility, no applicability | DiscardControlDraft | `ControlDraftDiscarded`; not visible, id stays occupied |
| open draft | ReviewControl | `ControlReviewed` (accept or request_changes) on the exact pending revision and latest decision |
| open draft with accepted review | ApproveControl | `ControlApproved`; reviewer, approver and owner separation checked against responsibilities and waivers |
| approved, no pending | ProposeControlSuccessor | `ControlSuccessorProposed` (revision + 1, companion revision) |
| approved, no pending | ProposeControlRetirement | `ControlRetirementProposed` |
| pending successor or retirement | WithdrawControlProposal | `ControlProposalWithdrawn`, approved content restored |
| pending retirement with accepted review | RetireControl | `ControlRetired`, terminal |
| open draft | DesignateControlOwnerPerson | `ControlOwnerPersonDesignated` (no-op when unchanged) |
| pending scope | AssignResponsibility / RevokeResponsibility | `ResponsibilityAssigned` / `ResponsibilityRevoked` |
| discarded or retired | any mutation | not found or state conflict, no fact |
| any | stale expected revision, wrong program | conflict or not found, no fact |
| any | writer holds an active source use | transient conflict, no fact |
| any | same CommandId, same authored intent | original result via native acceptance, no fact |
| any | same CommandId, changed intent | conflict via native acceptance, no fact |

## Qualification against done criteria

All criteria already had executable coverage except direct owner-level preparation admission, which gets a new aggregate-only class.

## Changes

No production change. No real defect was found: every `Append` refusal path (stale cut, active source use, missing header, bad authority, oversize payload, invalid binding) returns a `Result` failure before `RaiseEvent`, and the reducer re-runs the same `Validate` for replay. Added `ControlDraftAggregateTests.cs` for `Prepare` (identity occupancy, program scoping, stale revision, discard visibility), which had no direct test. The `ControlDraftTests.cs` file in scope covers `ControlRules`; it was left unchanged.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, normalized identifier, tenant/program scoping | `ShouldUseSameIdentityGivenNormalizedIdentifierWithinProgram` (existing) |
| Creation admitted only on empty owner | `ShouldAdmitCreationGivenEmptyOwner`, `ShouldConflictGivenExistingControlWhenPreparingCreation` (new) |
| Mutation of unknown or foreign-program control hidden | `ShouldReportNotFoundGivenEmptyOwnerWhenPreparingMutation`, `ShouldReportNotFoundGivenOtherProgramWhenPreparingMutation` (new) |
| Stale revision, competing decisions | `ShouldConflictGivenStaleExpectedRevisionAfterContentRevision` (new); `ShouldConflictWithoutAppendGivenPreparationBeforeSameRevisionAuthorityChange` (existing) |
| Current or omitted expected revision admitted | `ShouldAdmitMutationGivenCurrentOrOmittedExpectedRevision` (new) |
| Discard lifecycle, id stays occupied on replay | `ShouldHideControlGivenReplayedDiscardWhenPreparingMutation` (new); `ShouldDiscardUnpublishedDraftAndRejectFurtherEditsGivenExistingDraft` (existing) |
| No-op without fact | `ShouldPreserveDraftRevisionGivenUnchangedNormalizedContent`, `ShouldPublishOriginalResultEnvelopesGivenNoOpAndDecisionNativeCommands` (existing) |
| Exact retry after later facts | `ShouldReplayOriginalRevisionGivenLaterControlContentAndStableLogicalRequest` (existing); `ShouldReplayOnlyOriginalCreateGivenDuplicateIdentifier` (existing) |
| Invalid decisions append nothing | `ShouldRejectMalformedNativeReviewGivenExactPreparedContent`, `ShouldRejectPoisonedAfterAuthorityGivenVerifiedNativeChange`, `ShouldRejectChangedNoOpReceiptGivenUnchangedAuthorityHeader`, `ShouldRejectOversizedScalarEnvelopeGivenValidImmutableDraft`, `ShouldRejectInvalidIdentifierGivenOtherwiseExactNativeCreation` (existing) |
| Replay and bounded history | `ShouldRetainExactHistoryGivenManyNativeContentRevisions`, `ShouldRejectFurtherMutationGivenDiscardedNativeAuthority` (existing) |
| Separation of duties and waivers | `ShouldFreezeExactWaiverProofAndReplayAfterExpiryGivenAuthorizedSelfReview`, `ShouldRetainExistingDecisionPolicyGivenSameIndependentReviewerAndApprover` (existing) |

Malformed-history tests were not added, by user preference. Active source-use fencing in `Prepare`/`Append` has no direct ControlDraft test (it needs a granted source-use fact from another owner); the fence logic is `SourceUseReservations`, covered by its own tests.

## Event contract status

No event add, rename or removal is needed. The twelve events (`ControlDraftCreated`, `ControlDraftRevised`, `ControlDraftDiscarded`, `ControlReviewed`, `ControlApproved`, `ControlSuccessorProposed`, `ControlRetirementProposed`, `ControlRetired`, `ControlProposalWithdrawn`, `ControlOwnerPersonDesignated`, `ResponsibilityAssigned`, `ResponsibilityRevoked`) match the catalog baseline list. Facts carry compact header, content references and operation provenance, with bodies in immutable content.

Catalog note for the catalog owner, not changed here: `implementationStatus` still reads `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` and `baselineSourcePath` points at `Features/Controls/ControlDraft.cs`; the current path is `Features/Controls/Aggregates/ControlDraft.cs`.

## Later-stage follow-ups (not aggregate gaps)

- Handlers and HTTP routes for the selected command contracts (`ApproveControl` etc. use CommandId and original-result replay) stay in the existing handlers.
- Impact digest source checks, evidence/mapping/work/risk follow-ups belong to reactors and projections.
- Catalog status and path reconciliation above.

## Evidence

Tests are written, not executed; build and run pending. No RED run observed (no behavior defect was found, so the new tests are coverage additions). No Jev request was made for this slice. Helpers used (`ControlSourceFixture`, `ControlRules.Create/Revise/Discard`, `Source.Controls.RulesAsync`, `Source.Reader.HydrateAsync`) were read from existing tests but not compiled.

## Review before build

Independent review, 2026-10-29. Nothing compiled or executed.

Findings:
- Source: no defect. `Prepare` fences active source use first, then occupancy (creation), visibility and program (mutation, NotFound so no leak), then stale expected revision (Conflict, no fact). Event list matches the catalog baseline (12 events); no discrepancy, so no Choice question was needed.
- Tests: all seven assert real outcomes through replayed history. Weak point fixed: the stale-revision test asserted only that the message contained "2"; it now asserts "Current revision: 2" (the exact `VersionConflictRequestErrors` text). Helper signatures (`ControlRules.Create/Revise/Discard`, `RulesAsync(tenant, program, id)` resolves to the 4-argument overload, `ControlSeeds.AppendAsync`) were read and match.
- Compile: the using block is copied from a sibling file that compiles; `ControlSourceFixture` is internal and used only in private test bodies. Not compiled; build pending.

Jev (model jev-1.14.0, advisory; request and response in `implementation-jev/controldraft-test-impl-review-2026-10-29-*.json`):
- Discard visibility and occupancy test proves the rule: noul 0.90.
- Stale revision test proves refusal and revision report: noul 0.91.
- Program scoping test proves NotFound without leak: noul 0.82.
- Missing direct source-use fence test should not block verification: not_block 0.90 (confidence 0.85). Retained as a documented gap (covered by SourceUseReservations tests).

Remaining risks: build/run pending; catalog `implementationStatus` and `baselineSourcePath` reconciliation belongs to the catalog owner.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
