# Location aggregate review

Location owns one tenant-scoped location's revisioned record and its single pending change. The approved split leaves active normalized-name uniqueness to `LocationNameClaim`, keyed by tenant, immutable location kind and normalized name. A location change requiring a claim is coordinated through the existing name-change process; no other location's record belongs in this stream. The process, claim and immutable content references remain outside the Location owner.

The accepted change fences the expected revision and generation. Apply or cancel closes that pending operation, and replay enforces the matching owner, operation, intent and revision. The existing reducer did not check the tenant carried by `LocationRevisionRecorded`; the focused regression demonstrates a foreign-tenant revision being accepted on replay, and the Location reducer now rejects it. The owner keeps only its current and previous content references and one pending change; event-store history and its replay remain framework responsibilities and are not claimed to be bounded here.

The existing [Jev boundary request](jev-review-2026-10-26/boundary_fit-09.request.json) and [response](jev-review-2026-10-26/boundary_fit-09.response.json) include Location as item 1. Jev 1.14.0 assigned 0.86 probability to the selected boundary, while its separate evidence-sufficiency judgment was 0.51. This is advisory, not proof of correctness; no event names or contracts changed in this slice.

Validation: `NamedRecordProcessTests` passed 22/22 before the change. The new `LocationAggregateTests.ShouldRejectRevisionFromAnotherTenantGivenLocationReplay` failed before the reducer change and passed afterward. The combined Location and named-record process selection passed 23/23 in Release. These tests exercise the in-memory host and replay; they do not establish deployed-host, production-store, or cross-service recovery qualification. Name-claim authority and the full choreography remain separate review scopes.

## 2026-10-29 aggregate-stage completion pass (batch)

Verified against the re-polished `aggregates.json` Location entry (identity text, `RecordLocation` / `ReviseLocation` / `CancelLocationChange` / `ApplyLocationChange` contracts). Source was not changed: no real gap was found. Coverage was added.

### Identity and invariant

- **Identity:** one stream per tenant-scoped `locationId` (`InventoryRegisters.LocationAddress(tenantId, locationId)`). `RecordLocation.LocationId` is the caller-supplied immutable creation identity; a second creation on an occupied identity is refused, never forked to a new stream. Later changes are keyed by `CommandId`, carried as the operation's logical id and compared by `NativeAcceptance`; the expected revision is a concurrency precondition, not identity.
- **Named invariant:** one location record decides, atomically, its revision sequence and its single pending change (creation needs an empty identity, a revision needs the exact expected revision, at most one change pending, apply and cancel close only the exact pending operation and generation). Kind immutability and active-name uniqueness are not decided here (see exclusions).
- **Exclusions:** active same-kind name uniqueness belongs to `LocationNameClaim` (tenant, kind, normalized name); the name-change process, content normalization and authorization sit outside the owner.
- **Boundary decision:** keep. Revision and pending change must be decided together by one owner; the name claim is already split out and coordinated by `LocationNameClaimReactor`, `LocationChangeApplicationReactor` and `LocationOldNameReleaseReactor` (later stage).
- **History bound:** the owner keeps only its last two applied revision references and one pending change. Event-store replay length is a framework matter and not claimed bounded here.

### Lifecycle

| State | Command | Result |
| --- | --- | --- |
| No record (generation 0) | accept creation | `LocationChangeAccepted` (expected revision 0) |
| No record | accept revision | refused NotFound, no fact |
| Recorded, nothing pending | accept creation | refused Conflict, no fact |
| Recorded, nothing pending | accept revision at current revision | `LocationChangeAccepted` |
| Recorded | accept revision at stale revision | refused (stale), no fact |
| Pending | accept anything | refused Conflict (retryable), no fact |
| Pending | apply with exact root and intent | `LocationRevisionRecorded` (revision + 1) |
| Pending | apply with other intent / no pending | refused Conflict, no fact |
| Pending | cancel exact root | `LocationChangeCanceled`; late apply then refused (fence), next accept uses generation + 1 |
| Applied or no pending | cancel | refused Conflict, no fact (handler-level terminal no-op) |

Exact retry and changed-intent refusal are decided by `NativeAcceptance.Decide` over the full `AcceptedOperationBinding` using the logical id selected at hydration; the owner exposes the original accepted fact on replay even after later facts.

### Changes

- Source: none.
- Tests: `LocationAggregateTests` extended from one test to the set below (written, not executed).

### Test-to-criterion table

| Criterion | Test |
| --- | --- |
| Accepted creation, pending change | `ShouldHoldPendingChangeGivenAcceptedCreation` |
| One pending change, competing accept | `ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange` |
| Revision without record | `ShouldRefuseRevisionAndAppendNothingGivenUnrecordedLocation` |
| Occupied identity not re-created | `ShouldRefuseCreationAndAppendNothingGivenExistingLocation` |
| Stale revision | `ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedLocation` |
| Operation owner must be this stream | `ShouldRefuseOperationOfAnotherOwnerAndAppendNothingGivenForeignStream` |
| Apply with no pending | `ShouldRefuseApplyAndAppendNothingGivenNoPendingChange` |
| Apply with changed intent | `ShouldRefuseApplyAndKeepPendingGivenDifferentIntent` |
| Cancel fences late apply, next generation | `ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledChange` |
| Cancel after terminal outcome | `ShouldRefuseCancelAndAppendNothingGivenAppliedChange` |
| Bounded retained state | `ShouldKeepOnlyLastTwoRevisionsGivenThreeAppliedRevisions` |
| Valid replay (JSON round trip) restores revision and pending | `ShouldRestoreRevisionAndPendingChangeGivenValidHistoryReplay` |
| Original acceptance found after later facts | `ShouldResolveOriginalAcceptanceGivenReplayAfterLaterFacts` |
| Replay tenant check | `ShouldRejectRevisionFromAnotherTenantGivenLocationReplay` (existing) |

### Event contract status

`LocationChangeAccepted`, `LocationChangeCanceled` and `LocationRevisionRecorded` match the catalog (discriminators and versions). No event rename, addition or payload change is requested. Distinct location lifecycle facts remain the catalog's `OPEN_LifecycleFactsOtherInventories` decision, unchanged here.

### Later-stage follow-ups (not aggregate gaps)

- Handlers for `RecordLocation`, `ReviseLocation`, `CancelLocationChange` and `ApplyLocationChange` resolving exact retry versus changed intent through `NativeAcceptance`.
- `LocationNameClaimReactor`, `LocationChangeApplicationReactor`, `LocationOldNameReleaseReactor`.
- Immutable kind enforcement across revisions lives with content normalization and the name-claim key, not this owner.

### Evidence

Tests are written, not executed; build and run pending (no build, test or format was run in this batch session). No Jev call was made; no event names changed.

## Review before build (2026-10-29)

Independent review of the tests and source before any build. Source `Location.cs` is unchanged; `NamedRecord` guards were read as the compiler would.

### Findings and fixes

- **Test defect (fixed):** `ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledChange` canceled a *creation* and then expected a second creation on the same id to succeed. `NamedRecord.PrepareChange` refuses creation once `Generation > 0`, so the test would have failed. It is replaced by `ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledRevision` (recorded location, revision accepted, canceled, late apply refused, next revision accepted as generation 3, five facts in order).
- **Gap in coverage (fixed):** new `ShouldRefuseCreationAndAppendNothingGivenCanceledCreation` pins the actual behavior: a canceled never-applied creation leaves the identity occupied (creation Conflict, revision NotFound, no new fact).
- Other 12 tests were traced against `NamedRecord` and `NativeAcceptance`; each asserts events or replayed state, and fixtures (`Ref`, `Author`, `At`, ulong expected revisions, owner streams) are valid. No malformed-history tests.

### Jev (jev-1.14.0, advisory), `implementation-jev/location-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability |
| --- | --- | --- |
| Fence test asserts the invariant (Noul) | true | 0.94 |
| Stale-revision test asserts the invariant (Noul) | true | 0.90 |
| Canceled creation id: keep_identity_burned / allow_new_creation / needs_source_resolution (Choice) | keep_identity_burned | 1.00 / 0.00 / 0.00 |
| Premise: canceled creation leaves identity non-empty (Noul) | true | 0.93 |
| Revision payload TenantId/LocationId: keep_as_cataloged / remove / needs_source_resolution (Choice) | keep_as_cataloged | 1.00 / 0.00 / 0.00 |
| Premise: those fields repeat stream identity (Noul) | true | 0.95 |

### Discrepancies and disposition

- Canceled-creation identity burn (model silent): Jev decisive, keep. Applied as a test of current behavior; no source change.
- `LocationRevisionRecorded` repeats tenant and location id already carried by the stream: Jev decisive to keep as cataloged. No change; shared event contract is out of scope. Noted for the catalog owner only as an observation, not a requested change.

### Remaining risks

- Nothing built or run. Compile risk is low: `RequestErrorKind` is used unqualified by sibling tests with the same usings; I could not confirm its namespace by grep.
- Exact retry versus changed intent (`NativeAcceptance.Decide`) is handler-stage and untested here.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
