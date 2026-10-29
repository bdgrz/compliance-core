# ExaminationEngagement aggregate review (2026-10-29)

Backlog group 72. Verified against the current `aggregates.json` entry (polished 2026-10-29). Aggregate stage only.

## Boundary decision

- **Identity:** deterministic stream `(tenantId, "examinations", examinationId)`. `ProgramId` is immutable once created. Creation is identified by the caller-supplied `ExaminationId`; Revise, Freeze and Amend are identified by their `CommandId` (Freeze and Amend carry the baseline id as the operation logical id), with the expected revision as a precondition, not an identity. No caller key forks the stream.
- **Invariant:** Freeze locks Type I setup to the exact approved boundary and complete source coverage; amendments preserve the prior baseline and enumerate changed sources. In the aggregate: one engagement is created once; a draft is revised only at the current revision; a frozen engagement changes only through an attributable baseline amendment whose revision is the previous baseline revision plus one; program, engagement type and boundary never change after creation.
- **Exclusions:** the full terms, source lists, preview coverage (every source family covered, approved boundary reference) and baseline snapshots are immutable content prepared by the handler and the rule class (`ExaminationEngagementRules`) and referenced by the fact. Readiness, contact validation and preview preparation are handler-stage work. D01-03 (auditor-confirmed engagement date) and D01-11 (unknown, never placeholder) are product rules this owner decides on content; they are carried by the immutable content and its rules, not by new aggregate fields.
- **Decision:** keep as one aggregate (catalog `boundaryDecision: narrow`: setup and freeze commit one baseline authorization atomically; baseline history lives in immutable content, the current pointer and lock stay with the engagement). No split, no reactor. History is bounded by the engagement lifecycle (one create, drafts, one freeze, amendments); bodies are content refs, not inline history.

## Lifecycle

| State | Command | Result |
|---|---|---|
| no engagement | Create (ExaminationId) | `ExaminationEngagementOpened`, revision 1, draft |
| exists | Create | Conflict, no event |
| no engagement | Revise / Freeze / Amend | NotFound, no event |
| draft | Revise (expected revision = current) | `ExaminationEngagementRevised`, draft |
| draft | Freeze (baseline id, expected revision = current, baseline revision 1) | `TypeIBaselineFrozen`, baseline_frozen |
| draft | Amend | Conflict, no event |
| baseline_frozen | Revise or second Freeze | Conflict, no event |
| baseline_frozen | Amend (expected revision, baseline revision + 1) | `TypeIBaselineAmended`, baseline_frozen |
| any | stale revision, unversioned or foreign-stream operation, unfinalized native cut, logical id already accepted | refusal, no event |
| create | another program, non-Type I type, empty boundary | Validation, no event |

## Changes

- Create: `Accept` now refuses, with Validation and before any fact is raised, a prepared engagement whose program differs from the program the owner was opened under, whose type is not `type_i`, or whose boundary is empty. Previously the guard only checked for a non-empty program; the fold (`ApplyFact`) rejected type and boundary after `RaiseEvent`, and the program mismatch was not rejected anywhere on creation, so a mismatched view would have fixed the engagement under a program other than the one the stream was opened for.
- No other source change. Reducer guards for scope, root, actor, clock, revision, lifecycle and baseline lineage already reproduce the live guards on replay.

## Test-to-criterion table (`ExaminationEngagementAggregateTests`)

| Criterion | Test |
|---|---|
| Stable identity | ShouldAddressStreamByTenantAndExaminationGivenAnySelectedCommand |
| Create raises one fact; second create refused | ShouldRaiseOpenedFactGivenFirstCreateAndRefuseSecondCreate |
| Invalid create, no event | ShouldAppendNothingGivenCreateWithAnotherProgramOrNonTypeIOrNoBoundary |
| Missing engagement, no event | ShouldAppendNothingGivenReviseOfMissingEngagement |
| Stale revision, revise of replayed draft | ShouldReviseAndRefuseStaleRevisionGivenReplayedDraft |
| Operation provenance guards | ShouldRefuseGivenOperationUnversionedOrNotRootedOnOwnStream |
| Competing append / unfinalized cut | ShouldRefuseGivenAppendWithoutFinalizedNativeCut |
| Freeze once; revise and second freeze refused when frozen | ShouldFreezeBaselineOnceAndRefuseReviseOrSecondFreezeGivenFrozenEngagement |
| Amend lineage; amend before freeze refused | ShouldAmendBaselineGivenFrozenEngagementAndRefuseAmendmentBeforeFreeze |
| Baseline identity and revision bound to operation | ShouldAppendNothingGivenBaselineWhoseIdentityOrRevisionDiffersFromOperation |
| Replay of valid history | ShouldRestoreStateGivenValidHistoryThroughAmendment |
| Exact retry vs changed intent after later facts | ShouldRecognizeExactRetryAndRefuseChangedIntentGivenCommittedFactAfterLaterFacts |
| Logical id already accepted | ShouldAppendNothingGivenLogicalIdentityAlreadyAcceptedOnStream |

Existing coverage retained: `ExaminationEngagementTests` and `ExaminationEngagementNativeHistoryTests` (rule class, content/state faults, captured cut, baseline lineage beyond the former inline limit), `ExaminationEngagementCommandHandlerTests`, `ExaminationEngagementNativeBoundaryTests`, `ExaminationEngagementNativeHttpTests`. Malformed-history tests were not added (user preference).

## Event contract status

`ExaminationEngagementOpened`, `ExaminationEngagementRevised`, `TypeIBaselineFrozen` and `TypeIBaselineAmended` are the four catalog facts; none added, renamed or removed. The catalog still records `eventAuthority: retained_baseline_business_facts_requires_schema_reconciliation`: the baseline payload (`Engagement` view, `Baseline` view) is now carried as an immutable content ref (`Change`) plus native operation provenance, with tenant, program, revision, type, boundary and baseline id/revision as scalar business fields. That reconciliation is catalog work (event-catalog stage), not an aggregate gap. Identity fields repeat the stream key as in the baseline, and scalar fields mirror the content so replay can verify the content without reading it; flagged for the event-contract stage, not changed.

## Later-stage follow-ups

- Handler: complete source coverage, approved-boundary binding and unresolved-source blocking for Freeze, reason and changed-record rules for Amend, and the mutable readiness/contact/preview preparation stay in the handler and rule stage.
- Catalog `baselineSourcePath` lists `Features/Examinations/ExaminationEngagement.cs`; source now lives in `Aggregates/` (tracker note).
- Catalog event typed fields predate the content-ref payload (see above); reconcile in the event-catalog stage.
- Package-material impact of an amendment is a reactor/projection concern (audit package rebuild), not decided here.

## Evidence

Tests written, not executed; build and run pending. The source guard was written with its test (RED not run). Nothing was built, formatted, staged or committed. Jev not consulted (no event naming or boundary question).

## Review before build

Independent review of source and `ExaminationEngagementAggregateTests`; nothing built or run. The earlier "Jev not consulted" evidence line is superseded by this section.

### Findings and fixes

- Source: the create guard in `Accept` (program, `type_i`, boundary) is correct and ordered after `PrepareCreate` and before `RaiseEvent`; every lifecycle refusal path (stale revision, unversioned or foreign root, unfinalized cut, accepted logical id, baseline identity/revision mismatch, amend before freeze, revise or second freeze when frozen) was traced by hand to a Conflict, NotFound or Validation result with no fact. No further implementation gap found.
- Compile risk fixed: `Line.At` used no instance state (CA1822); made it `static` and called it as `Line.At(2)`.
- Style fixed: the `Amend` helper was a block body with a single return; converted to an expression body.
- Fixture values checked against validators: view and baseline constructor arities, `Uuid` actor ids parsed by `Member`, `UpdatedAt` equal to operation time, baseline revision equals previous plus one, hex hash strings of 64 characters.
- Not confirmed without a build: `DomainEventSeed.Attach` sharing the same attached fact instances across several replayed owners built from one `Line`; `Assert.Equal(Uuid, Uuid?)` inference; use of `internal` `ReplayCut.Confirm` (InternalsVisibleTo Tests is present).

### Jev (model jev-1.14.0; request/response in `implementation-jev/examinationengagement-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Create guard consistent with invariant (noul) | true | 0.92 |
| Revise or second Freeze on frozen engagement | conflict_no_fact | 1.00 |
| Retry handling consistent | consistent | 0.99 |
| Tests assert real outcomes (noul) | true | 0.84 |

No catalog discrepancy (event or command) was found, so no add/remove choice was asked. Answers are advisory and agree with the code.

### Remaining risks

Tests still unexecuted (RED/GREEN pending); catalog `baselineSourcePath` and typed event fields are stale (event-catalog stage, shared files).


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
