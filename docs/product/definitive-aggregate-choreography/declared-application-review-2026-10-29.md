# DeclaredApplication aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only (backlog group 68, "Source present; verify"). Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending. Verified against the current `aggregates.json` entry (polished 2026-10-29), `aggregate-details.md` and product rules D05-02 and D05-11.

## Boundary decision

**Keep together** (catalog `boundaryDecision: selected_split`, split from the legacy application ledger). One stream per application (`Tenant / applications / ApplicationId`) decides, atomically, the application's creation, metadata revision, retirement and import admission generation.

- Identity: tenant-scoped `ApplicationId`, the caller-supplied immutable creation identity. The stream address derives from tenant and application id only, so no request key forks a stream. Creation uses `ApplicationId` as its logical identity; later commands use `CommandId` bound to this stream and the expected revision. Every fact carries an operation whose owner must be this stream.
- Invariant: an application is declared once, revised only at its current revision, retired once (effective-dated, with a reason and, for a merge, an approved successor relationship for the exact current revision), and no local change is admitted while an import hold is unsettled or the identity is reserved by an import plan.
- Exclusions: name and alias uniqueness is decided by `ApplicationNameClaim`; import batch state by `ApplicationImportBatch`; source claims by `ApplicationImportSource`; system instances by `DeclaredSystemInstance`; relationships beyond the successor needed for a merge by `ApplicationRelationship`; list, preview and revision views are projections.
- Reactor/split: import target reservation, effect, fence and release are reactor commands received by this owner (`ImportTargetReservationReactor` and siblings); the batch decides, the reactors drive, this aggregate admits or refuses at its own revision. Metadata history is small and bounded by human edits; import effects and relationship state live in the partial files and are out of scope for this pass.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Declare, valid | revision 1 | `ApplicationDeclared` |
| none | Declare, invalid or incomplete when governed | validation refusal, no event | none |
| none, reserved by import plan | Declare | conflict, no event | none |
| none | Revise or Retire | missing record, no event | none |
| declared | Declare again | conflict, no event | none |
| declared | Revise, expected = revision, valid | revision + 1 | `ApplicationRevised` |
| declared | Revise, stale revision | version conflict, no event | none |
| declared | Revise, invalid content | invalid content, no event | none |
| declared | Retire, expected = revision, reason, effective date | revision + 1, retired | `ApplicationRetired` |
| declared | Retire without effective date (new guard) | invalid content, no event | none |
| declared | Retire with merge lacking approved successor for current revision | state conflict, no event | none |
| declared, import hold pending | Revise or Retire | transient state conflict, no event | none |
| retired | Revise, Retire | state conflict, no event | none |

Retired is terminal. Exact retry of a committed fact is decided by `NativeAcceptance.Decide` over the hydrated selected logical id, not by current content; a changed intent under the same id is `Conflict`.

## Changes

Adversarial qualification found two real gaps.

1. **Retirement without an effective date appended a fact.** D05-11 says retirement is effective-dated. `Retire` accepted `default(DateTimeOffset)` (the sibling `DeclaredSystemInstance` already refuses it). `Retire` now returns `InvalidContent` before appending, after the stale-revision, lifecycle and import-hold checks so those keep priority.
2. **Replay folds did not check fact identity.** `ApplicationDeclared`, `ApplicationRevised` and `ApplicationRetired` checked only the operation owner (stream) and not the payload tenant and application id against the tenant and id the aggregate was opened with. A new `RequireOwnIdentity` fold guard, mirroring the relationship and import-effect folds in the partial files, throws on a fact for another tenant or application. No malformed-history test was added, by user preference; valid replay is covered.

No other production change. Verified as already sound: governed-field completeness (D05-02) on declare and revise, size bounds, self-merge refusal, successor binding to the approved target revision, exact-retry decisions, stale revision handling, import hold refusal, and bounded history.

Not changed, by design: replay folds do not enforce strictly sequential revisions because historical streams carry `SystemInstanceDeclared` facts that advance the application revision; tightening that risks hydrating legacy streams.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, creation once, redeclare refused | `ShouldPreserveHistoricalApplicationRevisionGivenLegacyInstanceEvent`, `ShouldRestoreStateAndDecideRetriesGivenValidDeclaredRevisedAndRetiredHistory` |
| Invalid declaration appends nothing | `ShouldValidateRequiredFieldsGivenApplicationDeclaration`, `ShouldRejectIncompleteContentWithoutEventsGivenEnabledFreshDeclaration`, `ShouldRejectOversizedAdditionsWithoutEventsGivenFreshDeclaration` (existing) |
| Governed fields on revise, legacy replay | `ShouldReplayHistoricalClearsAndRetainAdditionsGivenOldSerializedRevision` (existing) |
| Classification and restriction retained | `ShouldPreserveDeclaredClassificationGivenRevision`, `ShouldRetainExplicitRestrictionGivenApplicationDeclarationAndRevision` (existing) |
| Revise or retire with no declaration | `ShouldAppendNothingGivenRevisionOrRetirementOfUndeclaredApplication` (new) |
| Retirement needs an effective date | `ShouldAppendNothingGivenRetirementWithoutEffectiveDate` (new) |
| Competing decisions, stale revision | `ShouldRefuseSecondRevisionGivenCompetingDecisionsAtSameExpectedRevision` (new); stale revise in the legacy-instance test (existing) |
| Valid replay, exact retry of declare after later facts, retry of revise and retire, changed intent, closed after retirement | `ShouldRestoreStateAndDecideRetriesGivenValidDeclaredRevisedAndRetiredHistory` (new) |
| Retirement reason, self-merge, stale, successor binding, terminal | `ApplicationRetirementTests` (existing, other file) |
| Import hold blocks local writes, identity reservation, replay of import facts | `DeclaredApplicationImportHoldTests` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

- Owned facts: `ApplicationDeclared`, `ApplicationRevised`, `ApplicationRetired` here; import effect facts (`ApplicationImportEffectPending`, `ApplicationImportEffectCanceled`, `ApplicationImportTargetAuthorized`, `ApplicationImportTargetSettled`) and relationship facts in the partial files. Payloads carry business facts plus native operation provenance and a frozen first response; actor is the stored actor reference.
- No event add, rename, remove or payload change is requested.

## Later-stage follow-ups (not aggregate gaps)

- `RetireApplicationHandler` preflights through `PrepareRetire`, which does not see the effective date; the refusal now comes from `Retire` inside the native operation. A handler or request validation should reject a missing `EffectiveAt` before the first response is frozen so a refusal does not follow response preparation.
- Handlers/reactors for import reservation, effect, fence and release remain in their own stages.
- Application lists, previews and revision views stay projections.
- Name claim acquisition before using a name (`ApplicationNameClaim`) is a separate planned flow, not asserted by this aggregate.

## Evidence

- Jev: no request this round; no naming or boundary question was open.
- Tests: written, not executed; build and run pending. Intended command: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~DeclaredApplication|FullyQualifiedName~ApplicationRetirement"`, then the backend unit suite.
- RED: none captured. The effective-date test fails on the previous code (retirement with a default date succeeded) but that run was not executed here, so it is not claimed.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review, 2026-10-29, before any build. Nothing was built or run.

### Findings

- No correctness gap found in the source or tests. Read as a compiler: the new tests use the `ApplicationAggregateExtensions` overloads (`Retire(expectedRevision, effectiveAt, reason, ...)`) and raw-operation overloads with the right argument order; `Assert.Single`, `.Count` on the list, `Version!`, `Error?.Kind` and `NativeFacts.Raised(seed)` (called once per replay from one seed owner, never re-attaching pending events) are all valid.
- Retire order: existence, retired, import hold and stale revision are decided before the effective-date refusal, so lifecycle refusals keep priority; the new test only exercises the declared, unheld, current case, so it can fail if the guard is removed.
- Replay test: captures `IsCreated`, `IsRetired` and `Revision` before the further commands in Act; `Decide` is called with a binding whose logical id equals the selected id for each replayed owner.
- No payload field repeats stream identity beyond the existing tenant/application ids carried by the pre-existing event contract; no event change requested.

### Fixes in this review

None required.

### Jev (advisory, model jev-1.14.0)

Request and response saved in `implementation-jev/declaredapplication-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
|---|---|---|
| Missing effective date on retire | refuse_no_event | 1.00 (confidence 1) |
| Second competing revise | version_conflict_no_event | 1.00 (confidence 1) |
| Tests assert the invariant and Test A can fail | true | 0.90 |
| Replay and retry decisions consistent with the replay rule | true | 0.92 |

No discrepancy between code and catalog (no event or command added or missing, no naming conflict, no identity-repeating payload field) was found, so no add_to_model Choice was needed.

### Remaining risks

- Tests and guards are written, not executed.
- `RetireApplicationHandler` preflight does not see `EffectiveAt` (later stage).
- Replay does not enforce sequential revisions because legacy `SystemInstanceDeclared` facts advance the revision.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
