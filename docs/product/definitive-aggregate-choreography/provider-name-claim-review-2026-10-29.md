# ProviderNameClaim aggregate review

## Identity, invariant and boundary

- **Identity:** the typed UUIDv5 of `("ProviderNameClaim", tenantId, normalizedName)` in area `provider-name-claims`, realm = tenant. Provider names are unique tenant-wide, so no kind participates. `AddressOf` now refuses an empty or non-normalized name, so `acme` and `ACME` cannot address two streams.
- **Named invariant:** one normalized active provider name within a tenant is held by at most one provider stream at a time, tagged with the change generation that last pinned it.
- **Exclusions:** provider content, revisions and the pending change (`Provider`); name-change sequencing (`ProviderNameChangeProcess`, receivers); rosters and name lookups (projections).
- **Decision:** keep as a separate aggregate (approved split, companion of `Provider`). Uniqueness is decided here, never by a projection. The shared `NameClaim` base owns occupancy and exact holder-generation rules; this type adds the provider-owner check.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| Free | `Claim` by a provider in the tenant | `ProviderNameClaimed` | non-provider, foreign-tenant or malformed holder (Validation); operation of another owner (Validation) |
| Held by H at g | `Claim` by H at generation >= g (re-pin) | `ProviderNameClaimed` | H at generation < g (Conflict); other holder (Conflict, taken) |
| Held by H at g | `Release` by exact H and g | `ProviderNameReleased` (-> Free) | other holder or generation (Conflict); foreign holder (Validation) |
| Free | `Release` | none | Conflict, not held |

Exact retry of a committed operation is decided by `Acceptance`: `Replayed` for an identical binding even after later facts, `Conflict` for changed intent.

## Changes (real gaps versus the sibling pattern)

- `AddressOf` validates input (normalized, non-empty name). No production caller passes unnormalized input (descriptor and test host normalize).
- `Claim` and `Release` refuse a holder that is not a provider stream (`providers` area, UUID resource) of the same tenant, with Validation and no fact. Previously any stream could take a provider name.
- Reducers refuse a claimed or released fact whose owner is not such a provider (valid history only; no malformed-history tests added).
- New `ProviderNameClaimTests` (test/Compliance.Tests/Features/Providers/Tests).

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity | `ShouldAddressSameStreamGivenSameTenantAndName`, `ShouldAddressDifferentStreamsGivenDifferentNameOrTenant`, `ShouldMatchDescriptorStreamGivenNormalizedName`, `ShouldRefuseAddressGivenNameThatIsNotNormalized`, `ShouldRefuseConstructionGivenStreamOfAnotherTenant` (plus existing vector test in `NamedRecordProcessTests`) |
| Success path | `ShouldAppendClaimedFactGivenFreeNameAndProviderInTenant`, `ShouldFreeNameForAnotherProviderGivenExactRelease` |
| Invalid decision appends nothing | foreign-tenant claim and release, non-provider stream, non-UUID provider resource, another owner's operation, release of a free name, release after release |
| Competing decisions | `ShouldRefuseClaimGivenNameHeldByAnotherProvider`, superseded generation claim, delayed release after re-pin |
| Re-pin | `ShouldRepinClaimGivenSameHolderAndLaterGeneration` |
| Replay | `ShouldRestoreHolderGivenValidClaimHistory`, `ShouldRestoreFreeNameGivenClaimThenRelease` |
| Exact retry after later facts | `ShouldReplayCommittedClaimGivenExactRetryAfterLaterFacts` |
| Changed intent | `ShouldConflictGivenSameLogicalOperationWithChangedIntent` |

`NamedRecordProcessTests` additionally cover process-level conflicts, concurrency, rename and release through the in-memory host.

## Event contract status

`ProviderNameClaimed` and `ProviderNameReleased` carry Owner, Revision, Generation, Intent, SourceProofs, Operation, FirstResponse, OperationResult; no metadata duplication. No event change needed. History is bounded: only current holder and generation are kept.

## Later-stage follow-ups (not aggregate gaps)

- Receiver behavior of `ClaimProviderName` / `ReleaseProviderName` on committed retry.
- Name-lookup projections of claims.

## Evidence

Tests written, not executed; build and run pending. The holder check and `AddressOf` guard were written with their tests (RED not run). Nothing was built, formatted or committed. Risk: if any existing process test claims with a non-`providers`-area holder, the new holder check would refuse it; the host uses real `Provider` streams.

## Review before build

Independent review, nothing built or run.

### Findings and fixes

- Compile error fixed: `ShouldMatchDescriptorStreamGivenNormalizedName` called `ProviderNames.Descriptor.ClaimStream(tenantId, "HQ")`, but the signature is `ClaimStream(Uuid, string? nameKind, string normalizedName)`. Now `ClaimStream(tenantId, null, "HQ")` (non-location kinds ignore the name kind).
- Source read as compiler: `Provider.Area`, `NameNormalization.Normalize`, `Uuid.TryParse(string, IFormatProvider, out)`, `Uuid.Empty` and the base `ObserveClaimed/ObserveReleased` all exist with the used signatures; usings added are used. No other defects found.
- Behavior: every accepted claim/release passes the holder check and the base `CheckClaim/CheckRelease`; refusals append nothing; exact retries are answered by `NativeAcceptance`; reducers re-enforce on valid history. Production callers build claims from streams via the descriptor, so the `AddressOf` guard does not affect them.
- Tests: all assert pending-event counts, holder/generation state or acceptance decisions; names and phase comments follow CNTRYL0001/0002; fixtures mirror the passing `LocationNameClaimTests` pattern.

### Jev (model jev-1.14.0 as returned, advisory)

Files: `implementation-jev/providernameclaim-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer |
|---|---|
| Competing-claim test asserts single holder | noul 0.93 |
| Delayed-release test asserts generation-exact release | noul 0.97 |
| Holder validation consistent with the invariant | noul 0.90 |
| Refusals as events: keep_no_event / add_to_model / needs_source_resolution | 0.95 / 0.01 / 0.04 (confidence 0.92); applied: no event |

No discrepancy with the approved catalog was found, so no event change is needed.

### Remaining risks

- Uncompiled and unexecuted; user build and test pending.
- Receiver retry behavior and name-lookup projection stay later-stage follow-ups.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
