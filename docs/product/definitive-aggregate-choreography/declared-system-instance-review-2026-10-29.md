# DeclaredSystemInstance aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (one stream per instance).** `DeclaredSystemInstance` (stream `system-instances/{systemInstanceId}` under the tenant) decides one instance's registered identity, immutable application parent, metadata revisions and terminal retirement, all against its own expected revision.

- Identity: tenant + caller `SystemInstanceId` (stable; the stream address is derived from it, no other key forks a stream). The parent `ApplicationId` is bound at registration and checked on every later command.
- Invariant: one system instance has one immutable application parent and registered identity; revisions and retirement follow its own expected revision. Kind is fixed at registration.
- Exclusions: parent application revision guard and active-application check (DeclaredApplication, handler-composed), provider-identifier uniqueness across instances (needs an authoritative claim, see open items), access-review scope decisions (SystemInstanceAccessReviewScope), rosters, lists and history (projections), authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Declare (own-stream root operation, bounded fields, named actor) | active, revision 1 | `SystemInstanceRegistered` |
| exists | Declare (same or changed content) | conflict, no fact (exact retry resolved in the operation layer) | none |
| active | Revise (expected revision, parent matches, bounded name/rationale, named actor) | revision + 1 | `SystemInstanceRevised` |
| active | Revise with stale revision | version conflict, no fact | none |
| active | Retire (expected revision, effective date, bounded reason, named actor) | retired, revision + 1 | `SystemInstanceRetired` |
| retired | Revise, Retire (any intent) | state conflict, no fact | none |
| none or wrong parent | Revise, Retire | missing record, no fact | none |
| any | foreign or empty-logical-id operation | validation, no fact | none |

## Changes

Real gaps found and fixed:

1. `Declare` never validated the actor or change time (`Revise` and `Retire` did). An empty member id, blank or oversized display, or default `ChangedAt` produced a registration fact; `RegisteredByMemberId` feeds separation of duties. Added the check in `Declare`, and the registered fact now stores the trimmed display (as the other two facts do).
2. The `SystemInstanceRegistered` replay guard checked only the double-create. Added tenant, instance id, revision 1, nonblank name/kind, nonempty actor and `ChangedAt` checks to the apply (the revision and retirement applies already validate).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, two instances independent of the parent revision | `ShouldKeepApplicationRevisionIndependentGivenTwoInstanceDeclarations` |
| Immutable parent, re-declaration refused, event contract | `ShouldOwnImmutableParentAndRevisionGivenDeclarationAndRedeclaration` |
| Revise preserves parent, replay of revision | `ShouldReviseInstanceWithoutChangingItsParentGivenCurrentRevision` |
| Stale revision, no fact | `ShouldRejectStaleInstanceRevisionGivenChangedContent` |
| Invalid declaration appends nothing (actor) | `ShouldRefuseDeclarationWithoutFactGivenUnattributedActor` (new, RED expected before fix 1) |
| Own-stream operation required | `ShouldRefuseForeignOperationWithoutFactGivenProvenanceOfAnotherOwner` (new) |
| Retirement, retry after commit, later revise refused | `ShouldRetireOnceAndRefuseLaterDecisionsGivenCommittedRetirement` (new) |
| Wrong parent, stale revision, blank reason on retire | `ShouldRefuseRetirementWithoutFactGivenWrongParentOrStaleRevisionOrBlankReason` (new) |
| Invalid revision content | `ShouldRefuseRevisionWithoutFactGivenBlankRationaleOrBlankName` (new) |
| No-op detection | `ShouldRecognizeUnchangedContentGivenRepeatedRevisionIntent` (new) |
| Replay of valid history to retired state, later commands refused | `ShouldRestoreStateAndRefuseChangeGivenReplayedHistoryWithRetirement` (new) |
| Retry of committed registration and competing stale revision after later facts | `ShouldRefuseDeclarationAndStaleRevisionGivenReplayedHistoryAfterLaterFacts` (new) |

Malformed-history tests were not added, by user preference. Exact-retry result replay (same id, same intent) is resolved by the operation layer through `Acceptance`, not by this aggregate; the aggregate refuses a second declaration or an already-retired retirement without a fact.

## Event contract status

- `SystemInstanceRegistered`, `SystemInstanceRevised`, `SystemInstanceRetired` carry business facts plus the native operation; no metadata duplication. No add, rename or remove requested.
- Payload change needed for rule D05-04 (see eventChangesNeeded): `Platform` and `Description` are not on `SystemInstanceRegistered`/`SystemInstanceRevised`.

## Open items (aggregate is partial)

- D05-04: instance kind must be one of cloud_account, source_code_organization, identity_provider_tenant, saas_tenant, environment, other; `other` needs a description; platform required except for environment and other. Source treats `Kind` as free text and has no platform or description, and many existing tests use kinds such as "production". Needs the event payload change and a migration decision for existing kinds; not guessed here.
- D05-05: provider identifier unique per tenant across (kind, platform, identifier) for active instances. This spans many streams and needs an authoritative claim/release owner or an admission design; a projection check is not acceptable. Not built.
- A repeated `Revise` with unchanged content still appends a revision if called directly; `IsUnchanged` is exposed for the handler to short-circuit. Left as is.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: apply `IsUnchanged` no-op short-circuit, authorization before replay, application revision guard (already composed in the declare handler).
- Projectors: instance lists, campaign scope references.

## Evidence

- Jev: not consulted this round; catalog and code were sufficient.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~DeclaredSystemInstanceTests"`.
- RED: not captured, not claimed. The unattributed-actor test was expected to fail before fix 1.
- Residual risk: uncompiled; the registered-apply guard could reject a hand-built legacy fixture if one replays into the aggregate with mismatched tenant or id.

## Review before build

Independent review, nothing compiled or run.

Findings
- Source: registration guard, actor validation, trimmed display and retirement/revision apply guards confirmed against the catalog invariant; no wrong or duplicate fact path found. Hand-built Registered fixtures in other tests (tenant, instance id and revision 1 match) satisfy the new apply guard.
- Tests: all assert events appended or not, state after replay, or failure codes. Names and Arrange/Act/Assert phases conform. One gap fixed: the revise test now asserts the Revised event preserves `Kind` ("production").
- Compile: helper overloads (`Declare/Revise/Retire` extensions, `ApplicationFacts.For`, `NativeFacts.Raised/Replay/Now/Ref`) exist with matching signatures; usings cover them. No `.Count()`, no re-attached events. Not confirmed by a compiler.
- Residual behavior: a direct `Revise` with unchanged content and a current revision still appends (handler uses `IsUnchanged`); stale retries are refused with VersionConflict.

Jev (jev-1.14.0; files in implementation-jev/declaredsysteminstance-test-impl-review-2026-10-29-*.json)
- Retire replay test asserts the invariant: noul 0.80 (accept).
- Retry after later facts test asserts it: noul 0.89 (accept).
- Kind/Platform/Description discrepancy (D05-04): event_change_needed 0.89 (decisive). Disposition: no aggregate change; payload change reported as eventChangesNeeded, not applied.
- TenantId/SystemInstanceId in payload: no_aggregate_change 0.97. Repo-wide convention, left alone.

Remaining: D05-04 event payload change and kind taxonomy/migration; D05-05 authoritative claim design; compile and test run pending.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
