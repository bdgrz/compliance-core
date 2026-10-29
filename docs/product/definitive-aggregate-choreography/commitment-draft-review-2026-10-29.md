# CommitmentDraft aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run pending.

## Boundary decision

**Keep as a single narrow owner (catalog `narrow`, retained).** One stream (`commitment-drafts/{draftId}` under the tenant) decides one commitment: immutable identity and source creation, revisioned authored content, an exact verified-source accepted review, independent approval publishing strictly later effective versions, and terminal retirement.

- Identity: tenant + program + normalized kind/identifier through the existing UUIDv5 `IdFor`; the supplied `DraftId` must equal it (checked in `Validate`). Later commands bind by `CommandId`. No request key forks the stream.
- Invariant (catalog): one tenant/program/kind/identifier has immutable identity/source creation, revisioned authored content, exact verified-source accepted review and independent approval that publishes strictly later effective versions; a retired commitment remains terminal.
- Kept together: pending authors, accepted review and current-revision responsibility assignments/decision constraints, because author/reviewer/approver independence must be decided atomically. Past version/decision lists and old scope sets are read-side (history directory, content store), not command state.
- Exclusions: provider/service existence, boundary categories, actor authorization, waiver proof lookup (admission in handlers and `CommitmentOperations`); version/decision listing and impact preview (projections).
- Alternatives: split (no, independence rules need one owner); reactor (no cross-owner decision).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Create, id equals `IdFor`, valid body | draft, revision 1 | `CommitmentDraftCreated` |
| none | any other command | not found | none |
| draft | Create exact retry (even after later facts) | original result | none |
| draft | Create changed intent | conflict | none |
| draft | Revise at current revision | revision + 1, acceptance cleared | `CommitmentDraftRevised` |
| draft | Revise stale revision, wrong program, blank body | refusal | none |
| draft | Review accept (verified current source, independent reviewer) / request changes | decision recorded | `CommitmentReviewed` |
| accepted review | Approve (later effective date, independent approver or waiver) | next effective version | `CommitmentApproved` |
| effective | Retire (later effective end) | terminal | `CommitmentRetired` |
| draft/effective | Assign / revoke responsibility on current pending revision | scope updated; unchanged current scope accepts as no-op receipt | `ResponsibilityAssigned` / `ResponsibilityRevoked` |
| retired | any change | conflict | none |
| any | Append with stale prepared sequence or physical position | transient conflict | none |

## Changes (real gaps)

No source change. `Prepare`, `Append`, `Validate` and `ExpectedAuthority` already refuse every invalid decision before `RaiseEvent`, bind native acceptance to the selected logical id, and re-run the same invariants on replay (`ApplyFact` calls `Validate`). The gaps were owner-level test coverage for:

- the `Prepare` lifecycle matrix;
- retry of a committed create/revise after later facts;
- changed-intent refusal on a reused revise `CommandId` after a later revision;
- stale revision and invalid revise appending nothing;
- competing append with a stale prepared sequence or physical position, and a second append of the same prepared fact.

## Test-to-criterion table

New owner-level file `CommitmentDraftOwnerTests`; existing cases in `CommitmentDraftTests` (rules-level create/revise/validation), `CommitmentDraftReplayTests` (handler create replay, retired service), `CommitmentNativeContentTests` (display-rename retry, waiver replay, malformed acceptance refusal), and the Review/Approval/Retirement test classes are retained.

| Criterion | Test |
|---|---|
| Lifecycle admission (`Prepare`) | `ShouldRefuseOnlyMissingOrConflictingTargetGivenPrepareAcrossLifecycle` |
| Exact retry of create after later facts | `ShouldReturnOriginalCreateWithoutNewFactGivenExactRetryAfterLaterRevisions` |
| Exact retry and changed intent after later facts | `ShouldReturnOriginalRevisionAndRefuseChangedIntentGivenRetryAfterLaterRevision` |
| Invalid decisions append nothing, stale revision | `ShouldAppendNothingGivenStaleRevisionOrBlankStatement` |
| Competing appends, stale stream position | `ShouldRefuseCompetingAppendWithoutFactGivenStalePreparedSequenceOrPosition` |
| Replay/hydration of valid history | hydration in every `OwnerAsync` assertion above; `ShouldRetainVerifiedHistoryOutsideCompactOwnerGivenManyCommitmentRevisions` |
| Create retry, changed create, retired service | `ShouldReplayOriginalCreateAndRejectNewDraftGivenRetiredService` |
| Review/approval independence, source verification, retirement, responsibilities | `CommitmentReviewTests`, `CommitmentApprovalTests`, `CommitmentRetirementTests` |
| Malformed native acceptance refused with no fact | `ShouldRejectMalformedNativeAcceptanceGivenExactCommitmentSourceCut` |
| Bounded history | compact header; bodies and authority are content refs; `Append` enforces `DomainEventPayload.MaximumBytes`; `ShouldCreateLegalLargeDraftGivenEscapedStatementAndContext` |

Malformed-history tests were not added, by user preference.

## Event contract status

Seven events (`CommitmentDraftCreated`, `CommitmentDraftRevised`, `CommitmentReviewed`, `CommitmentApproved`, `CommitmentRetired`, `ResponsibilityAssigned`, `ResponsibilityRevoked`) carry business facts, compact header, content refs and native operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: boundary-category validation on create/revise/approve and recording the boundary version id on approval (D09-04), typed source citation, owner Person and categories (D09-01/03) are not in the current event payloads; these are catalog `decides` items for the handler/event-schema stage, flagged for the catalog owner rather than invented here.
- Projections: version/decision directories, impact preview.
- Catalog status `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` and stale `baselineSourcePath` (`.../Commitments/CommitmentDraft.cs`) are shared-file items.

## Evidence

- Jev: not consulted; no open design decision.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Commitment"`.
- RED: none captured; not claimed. The new tests cover existing behavior and are expected to pass on first run.
- Compile risks to watch: `Assert.Equal(first.Value, retry.Value)` on the registration record, the `RulesAsync(tenant, program, id)` overload, and the `Conflict` kind for changed-intent revise (taken from the sibling test in `CommitmentNativeContentTests`).

## Review before build

Independent review of source and tests; nothing compiled or run.

### Findings and fixes

- Source: re-read `Prepare`, `Append`, `Validate`, `ExpectedAuthority`, `ValidateLocal`, `ValidateDecision`. Every transition (revise gate, review revision/verified source, approval accepted-review binding and strictly later `EffectiveFrom`, retirement, responsibility scope) is checked before `RaiseEvent` and again on replay. Stale sequence or physical position is a transient conflict before any state change. No source change needed.
- Test defect fixed: `ShouldRefuseCompetingAppendWithoutFactGivenStalePreparedSequenceOrPosition` ended with `Assert.Single(await FactsAsync(...))`, which reads the event store; the owner is never saved in that test, so it could not fail. Replaced with the owner's raised-event count (`AggregateScenario<CommitmentDraft>(owner).PendingEvents.Count == 1`), proving the valid append raised exactly one event and the competing duplicate raised none. Added `using Cntryl.Portia.Testing;`.
- Compile check: fixture helpers (`CommitmentSourceFixture`, `CommitmentSeeds.FreezeAsync`, `ProductClientServiceSeeds.WriteAsync`, `RulesAsync(tenant, program, id)`, `rules.Review`, `OwnerProvenance.For`, `CommitmentMutationContent`) match the signatures in the repo, and the competing-append arrange block is identical to the existing `ShouldRejectMalformedNativeAcceptanceGivenExactCommitmentSourceCut`. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002.
- Known weak spots left: the stale-revision and blank-body refusals assert only `IsSuccess == false` (error kind mapping from `CommandFailure` not confirmed from source); the `Prepare` matrix does not cover the retired branch (covered by Retirement tests).

### Jev (model jev-1.14.0, advisory), `implementation-jev/commitmentdraft-test-impl-review-2026-10-29-*.json`

| Question | Answer |
|---|---|
| Revise retry after later revisions acknowledged without new fact, changed intent refused | Noul 0.93 true |
| Stale/blank revise appends nothing | Noul 0.94 true |
| Competing/stale append adds no fact, valid adds one | Noul 0.92 true |
| Prepare matrix consistent with lifecycle gate | Noul 0.81 true |
| D09-01/03/04 missing event fields disposition | Choice `event_fields_later_stage` 1.00 (change_aggregate_now 0, remove_from_catalog 0, needs_source_resolution 0) |

Discrepancy disposition: typed source citation, owner Person, categories and boundary version id on approval are decided by the catalog but absent from the events. Jev is decisive that this is a later event-schema/handler-stage item; no aggregate change. Reported under remaining for the catalog owner.

### Remaining risks

- Not compiled or executed. Residual compile risk: the `Assert.Equal` on `CommitmentDraftRegistration`, and the `PendingEvents` use via `AggregateScenario`.
- Conflict kind for a changed-intent revise is taken from `CommitmentNativeContentTests`.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
