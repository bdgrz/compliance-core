# Policy aggregate review

Reviewed 2026-10-29 against the catalog row `Policy` (aggregates.json, polished 2026-10-29; decision D12-02). Scope is the aggregate stage only. Source: `src/Compliance.Core/Features/Policies/Aggregates/Policy.cs`.

## Identity, invariant, exclusions

- **Identity:** stream `tenant/policies/<PolicyId>`; `PolicyId` is the unchanged UUIDv5 of tenant, program and normalized identifier (`PolicyRules.IdFor`). `Append` and replay (`Validate`) both require `Id == IdFor(TenantId, ProgramId, Identifier)`, so no caller key forks a stream. Mutations are bound by `CommandId` through `NativeAcceptance`; the expected revision (or current-version gate for periodic review, successor and retirement proposal) is a concurrency precondition, not identity.
- **Named invariant:** one pending revision or retirement; immutable approved versions form one successor line; activation needs the latest accepted independent review plus independent approval; approved or retired content is not editable. Audience (D12-02): each version carries exactly one audience kind, and a role-targeted version names a non-empty team list (enforced by `ValidateContent` on every authored body in `Append`).
- **Exclusions:** policy acknowledgement and distribution campaigns, applicability target ownership, waiver issuance (`SeparationOfDutiesWaiver`), full version/decision history (verified immutable readers and projections).

## Boundary decision

Keep together, narrow (matches catalog `narrow`): draft authors, accepted review and approval must be decided atomically to prevent approving changed or self-reviewed terms. The aggregate keeps only a compact header plus the current authority reference; bodies, old versions and decisions live in verified immutable content, so history growth is bounded in command state. No split or reactor is needed.

## Lifecycle

| State | Decision | Result |
| --- | --- | --- |
| absent | Create | `PolicyDraftCreated` |
| existing (any, including discarded) | Create | conflict in `Prepare`, no fact |
| open draft | Revise | `PolicyDraftRevised`, clears accepted review |
| open draft or pending retirement | Review | `PolicyReviewed` (accept or request changes) |
| open draft with accepted review | Approve | `PolicyApproved` (independent approver or exact waiver) |
| approved, no draft/retirement | Successor / Retire proposal / Periodic review | `PolicyDraftRevised` / `PolicyRetirementProposed` / `PolicyPeriodicReviewConfirmed` |
| pending retirement with accepted review | Approve retirement | `PolicyRetired` (terminal) |
| unreviewed initial draft, no applicability | Discard | `PolicyDraftDiscarded` (not visible) |
| discarded or other program | any non-create | not found, no fact |
| retired | any | rewrite refused |

## Changes this round

- No production change: all lifecycle, identity, replay and OCC criteria already hold in source.
- Gap (coverage only): `Prepare` (creation conflict, unopened, wrong program, discarded) had no direct test. Added `ShouldRefuseCreationAndHideDiscardedPolicyGivenPreparationOfCommandsAgainstOccupiedIdentity`.

## Done-criteria status

| Criterion | Status | Evidence |
| --- | --- | --- |
| Stable identity | holds | `ShouldRejectAuthoredPolicyIdGivenIdentifierIdentityMismatch` |
| Named invariant, independence and waiver | covered | `ShouldRejectReviewGivenReviewerAuthoredRevision`, `ShouldRejectApprovalGivenApproverAuthoredOrReviewedRevision`, `ShouldAllowAuthorApprovalGivenExactPolicyWaiver`, `ShouldFreezeExactPolicyWaiverAndReplayOriginalDecisionGivenLaterExpiry` |
| Lifecycle (versions, successor, retirement, discard, periodic review) | covered | `PolicyLifecycleTests` |
| Invalid decisions append no events | covered | `ShouldRejectInvalidNativePolicyBindingGivenExactPreparedSourceCut`, new Prepare test |
| Exact retry, changed intent, retry after later facts | covered | `ShouldReplayOriginalResponseBeforeMutablePreparationGivenUnreadablePolicyHistory` |
| Stale revision, competing decisions | covered | `ShouldRejectStaleRevisionGivenConcurrentDraftEdits`, `ShouldConflictWithoutAppendGivenPreparationBeforeSameRevisionPolicyReview` |
| Replay of valid history | covered | `ShouldReplaySameVersionsAndDecisionsGivenRoundTrippedEvents` |
| Audience (D12-02) and content bounds | covered | `ShouldRejectDraftGivenInvalidAudienceCadenceOrContent`, `ShouldRejectDraftGivenBodyBeyondEventPayloadBound` |
| Bounded history | covered | `ShouldRetainVerifiedHistoryOutsideCompactOwnerGivenManyNativePolicyRevisions` |

## Event contract status

Eight facts (`PolicyDraftCreated`, `PolicyDraftRevised`, `PolicyReviewed`, `PolicyApproved`, `PolicyPeriodicReviewConfirmed`, `PolicyRetirementProposed`, `PolicyRetired`, `PolicyDraftDiscarded`) are unchanged. Catalog status is `retained_baseline_business_facts_requires_schema_reconciliation`; no event change is requested by this review. Observation: facts carry both `Actor` and `ActorMemberId` plus operation provenance; reconciliation against the schema catalog is a shared-file concern.

## Later-stage follow-ups (not aggregate gaps)

- Catalog `baselineSourcePath` still names `Features/Policies/Policy.cs`; refresh in the shared catalog.
- Audience-kind carried onto the approved-version view for acknowledgement campaigns (projector/PolicyDistribution).

## Evidence

Tests are written, not executed; build and run pending. Jev was not consulted: no open design decision or event-name change arose.

## Review before build

Independent review 2026-10-29; nothing compiled or run.

### Findings

- The new Prepare test was read as a compiler would: `PolicyRules.Create`, `Discard`, `PolicySourceFixture.SeedAsync`, `Policies.RulesAsync(tenant, program, id)`, `Reader.HydrateAsync` and `Result.Error!.Kind` all match their signatures and existing usage. `Discard` leaves `_draft` set, so `PolicySeeds.SaveAsync` final hash check holds, and `PolicySeeds.Metadata` already maps `PolicyDraftDiscardedContent`. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002; all ids are Uuid values and the identifier `NATIVE-PREPARE` is a valid normalized identifier.
- Test asserts real outcomes (refusal kinds per state, visibility, sequence after replay). It would fail if Prepare allowed non-create on a discarded or other-program policy, or create on an occupied identity. No finding requiring a code change; no weakened assertions.
- Implementation: `Append` re-derives identity, enforces actor binding, prepared cut (sequence plus physical position) and digest equality with the original receipt, so stale or competing input conflicts without a fact. Not re-verified this round: exact retry acknowledgement lives in `NativeAcceptance` and existing tests.
- No production or test edit was needed in this review.

### Jev (jev-1.14.0, advisory; request/response in `implementation-jev/policy-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability | Disposition |
| --- | --- | --- | --- |
| Create on discarded identity | conflict_no_fact | 1.00 | Matches code and test |
| Premise: discarded identity stays occupied | true | 0.95 | Holds |
| Test fails if Prepare allowed non-create on discarded/other program | true | 0.93 | Test is meaningful |
| Actor snapshot plus member id duplicating provenance | keep | 0.96 (confidence 0.94) | Keep; match check in `Append` guards mismatch. Schema reconciliation stays a shared-catalog follow-up, no event change requested |

No discrepancy between code and approved catalog was found (eight facts unchanged).

### Remaining risks

- Tests are written, not executed; the first build/test run is the evidence.
- Assumption that seeding a discard through `PolicySeeds` after `RulesAsync` reload works rests on reading `PolicySeeds.SaveAsync`; unconfirmed until run.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
