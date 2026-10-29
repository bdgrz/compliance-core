# LocationNameClaim aggregate review

## Identity, invariant and boundary

- **Identity:** the typed UUIDv5 of `("LocationNameClaim", tenantId, locationKind, normalizedName)` in area `location-name-claims`, realm = tenant. This matches `NamedRecordKindDescriptor.ClaimStream` for locations (kind participates only for locations). No caller key forks the stream: `AddressOf` now refuses an empty or non-normalized name, so `hq` and `HQ` cannot address two streams.
- **Named invariant:** one normalized active location name, within its location kind and tenant, is held by at most one location stream at a time, tagged with the change generation that last pinned it.
- **Exclusions:** location content, revisions and the pending change (`Location`); name-change sequencing, apply and cancel (`LocationNameChangeProcess`, receivers); parent rosters and name lookups (projections).
- **Decision:** keep as a separate aggregate (approved split in `aggregate-split-targets.json`, companion of `Location`). Coordination is via the existing process and receivers; uniqueness is decided here, never by a projection. The shared `NameClaim` base owns occupancy and exact holder-generation rules; this type adds only the location-owner check.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| Free | `Claim` by a local location | `LocationNameClaimed` | non-location, foreign-tenant or wrong-area holder (Validation); operation of another owner (Validation) |
| Held by H at generation g | `Claim` by H at generation >= g (re-pin) | `LocationNameClaimed` | H at generation < g (Conflict, superseded); other holder (Conflict, taken) |
| Held by H at g | `Release` by exact H and g | `LocationNameReleased` (-> Free) | other holder or generation (Conflict); foreign holder (Validation) |
| Free | `Release` | none | Conflict, not held |

Exact retry of a committed operation is decided by `Acceptance` (selected logical id): `Replayed` for an identical binding even after later facts, `Conflict` for changed intent. The receivers consult it before calling the aggregate.

## Changes

- `LocationNameClaim.AddressOf` validates input: null kind, empty name or a name that is not `NameNormalization.Normalize`d is refused with `ArgumentException`. This closes the one place a caller key could fork the stream. No source caller used it with unnormalized input (the production path uses the descriptor with names already normalized by `ReadName`).
- New `LocationNameClaimTests`.

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity | `ShouldAddressSameStreamGivenSameTenantKindAndName`, `ShouldAddressDifferentStreamsGivenDifferentKindOrTenant`, `ShouldMatchDescriptorStreamGivenNormalizedName`, `ShouldRefuseAddressGivenNameThatIsNotNormalized` (new behavior), `ShouldRefuseConstructionGivenStreamOfAnotherTenant` |
| Success path | `ShouldAppendClaimedFactGivenFreeNameAndLocalLocation`, `ShouldFreeNameForAnotherLocationGivenExactRelease` |
| Invalid decision appends nothing | `ShouldRejectHolderGiven...` (foreign tenant claim and release, non-location), `ShouldRefuseOperationGivenAnotherOwnerStream`, `ShouldRefuseReleaseGivenNameThatIsFree` |
| Competing decisions | `ShouldRefuseClaimGivenNameHeldByAnotherLocation` (scenario is a second, distinct location claiming a held name), `ShouldRefuseClaimGivenSameHolderAndSupersededGeneration`, `ShouldRefuseReleaseGivenDelayedGenerationAfterRepin` |
| Re-pin / no-op semantics | `ShouldRepinClaimGivenSameHolderAndLaterGeneration` |
| Replay of valid history | `ShouldRestoreHolderGivenValidClaimHistory`, `ShouldRestoreFreeNameGivenClaimThenRelease` |
| Exact retry after later facts | `ShouldReplayCommittedClaimGivenExactRetryAfterLaterFacts` |
| Changed-intent refusal | `ShouldConflictGivenSameLogicalOperationWithChangedIntent` |

Existing `NamedRecordProcessTests` additionally cover process-level conflicts, concurrency, rename ordering, release, retirement and kind-scoped uniqueness through the in-memory host. No malformed-history tests were added (user preference).

## Event contract status

`LocationNameClaimed` and `LocationNameReleased` carry `Owner`, `Revision`, `Generation`, `Intent`, `SourceProofs`, `Operation`, `FirstResponse`, `OperationResult`; no business fact is duplicated from metadata. No event change is needed from this review. History is bounded: the aggregate keeps only the current holder and generation.

## Later-stage follow-ups (not aggregate gaps)

- Handler/receiver behavior on a committed retry through `ClaimLocationName` / `ReleaseLocationName` is receiver-stage and already covered by the shared receivers.
- Name-lookup projections of claims remain projector work.

## Evidence

Tests written, not executed; build and run pending. The `AddressOf` guard was written together with its test (RED not run). Nothing was built, formatted or committed in this session.

## Review before build (2026-10-29)

Independent review of source, tests and this file before any build. Nothing was built, run, formatted or committed.

### Findings

- **Compile risk (fixed):** `PendingCount` used `PendingEvents.Count` as a property. Every existing test uses `PendingEvents.Count()` (`OperationalProcess` tests), so the property form is unconfirmed. Changed to `.Count()`.
- **Confirmed by reading:** `DomainEventSeed.Attach<T>(T, Uuid, ulong)` matches the `ulong` helper; `AcceptedOperationBinding`, `NativeAcceptanceDecision` and `OperationProvenance` live in `NativeOperations.Models` (imported); `OperationProvenance.AuthoredIntentSha256` exists; `LocationNames.Descriptor` is public in `TechnologyInventory.Processes`; hex fixture hashes are valid; the `OperationalProcess` stream (`operational-process-*`) is rejected by the `location-` prefix check; seeded operations are owned by the claim stream so the reducers' owner checks pass.
- **Implementation:** no gap found. Claim and release refuse non-local holders and foreign operations before any fact; taken, superseded and delayed-release cases are decided by the shared `NameClaim` rules; `AddressOf` has no production caller with unnormalized input (the descriptor path is used). Event payloads carry no field duplicating metadata. Re-pin at the same generation by a distinct operation appends a fact by design (exact retry is handled by `Acceptance`).
- **Tests:** each asserts an appended or not-appended outcome and replayed state; none is structural only. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002.

### Discrepancies

None between catalog and code: `LocationNameClaimed` and `LocationNameReleased` are registered in the catalog events. One code behavior the model does not state is the `AddressOf` normalization guard.

### Jev (model jev-1.14.0, advisory)

Request and response: `implementation-jev/locationnameclaim-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer |
|---|---|
| Tests cover the invariant (noul) | 0.94 |
| Delayed release refused is consistent (noul) | 0.97 |
| Exact retry after later release is Replayed (noul) | 0.84 |
| AddressOf guard disposition (choice) | keep_guard 0.94, needs_source_resolution 0.05, remove_guard 0.01 (confidence 0.91) |
| Premise: guard only rejects values outside the identity (noul) | 0.46, near-even, unresolved |

Disposition: keep the guard (decisive). The premise question was near-even; the guard is kept on the choice answer and on the identity definition in the catalog, and the premise answer is not treated as approval.

### Remaining risks

- Compile and test execution still pending (user builds once).
- No event, schema or catalog change needed.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
