# OperationalProcessNameClaim aggregate review

Verified against the 2026-10-29 catalog (`aggregates.json`, entry `OperationalProcessNameClaim`, status `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`, identity revision 2026-10-29). Scope is the aggregate stage only.

## Boundary decision

- **Identity:** one stream per `(tenantId, normalizedName)`. The resource is `AggregateIdentity.Create("OperationalProcessNameClaim", tenantId, normalizedName)`; `NamedRecordKindDescriptor.ClaimStream` derives the same address for non-location families, and `AddressOf` is the aggregate-side derivation. The constructor requires the tenant realm and the `operational-process-name-claims` area. No caller request key takes part, so none can fork the stream. The constructor cannot re-derive the name from a resource, so the admitting reactor stays the only source of the normalized name (same as the provider and location claims).
- **Named invariant:** authoritative active normalized-name uniqueness. At most one operational process stream holds the name, tagged with the change generation that last pinned it.
- **Exclusions:** name normalization and the active/kind policy (admitting reactor), the process content and version lifecycle (`OperationalProcess`), applying the change after the claim is held (`OperationalProcessChangeApplicationReactor`), and receiver cursors and retry metadata (technical state, not an event).
- **Keep/split/reactor:** keep as the selected split owner. History is bounded by name churn (claim, re-pin, release per change of one process), and contention is per name, never per tenant. Parent rosters and name indexes are projections and cannot answer admission.

## Lifecycle

| State | Command (event) | Result |
| --- | --- | --- |
| Free | `Claim` by a process holder in the tenant (`OperationalProcessNameClaimed`) | Held by holder at generation |
| Held by H at G | `Claim` by H at generation >= G (`OperationalProcessNameClaimed`) | Re-pinned at the new generation |
| Held by H at G | `Claim` by H at generation < G | Conflict, no fact (superseded) |
| Held by H at G | `Claim` by another holder | Conflict, no fact (taken) |
| Held by H at G | `Release` by exactly H at G (`OperationalProcessNameReleased`) | Free |
| Free or held otherwise | `Release` by another holder or generation | Conflict, no fact |
| Any | Holder not an operational process of the tenant | Validation, no fact |
| Any | Operation without a logical id or owned by another stream | Validation, no fact |
| Any | Retry of an accepted logical operation | Exact retry: success, no fact. Any changed field: Conflict, no fact |

## Changes in this round

- **Gap fixed: exact retry at the owner.** `Claim` and `Release` evaluated a retried logical operation against current state. A retry of an accepted claim re-pinned and raised a second fact carrying the same logical id, which the acceptance reducer treats as stream corruption; a retry of a claim after a later release would have raised a new claim. A retry of a release after a later claim or release was refused as "not held". `DecideRetry` now resolves an already accepted logical operation first: the full `AcceptedOperationBinding`, holder, record revision, generation and intent must all match for a success without a new fact, otherwise a non-retryable Conflict. A new logical operation takes the existing path.
- Existing holder-tenant checks (claim, release and both reducers) are unchanged.
- No event, schema or payload change.

## Test-to-criterion table

All tests are in `test/Compliance.Tests/Features/TechnologyInventory/Tests/OperationalProcessNameClaimTests.cs`.

| Criterion | Test |
| --- | --- |
| Stable identity | `ShouldKeepOneStreamPerNameGivenSameTenantAndNormalizedName`, `ShouldRejectStreamGivenAnotherTenantOrAnotherArea` |
| Authorized success, fact content | `ShouldHoldNameAndRaiseOneFactGivenFreeNameAndProcessHolder` |
| Tenant and owner-type refusal, no fact | `ShouldRejectForeignTenantHolderGivenOperationalProcessNameClaim`, `ShouldRejectHolderThatIsNotAProcessGivenOperationalProcessNameClaim`, `ShouldRejectForeignTenantHolderGivenOperationalProcessNameRelease` |
| Own logical operation required | `ShouldRejectClaimAndReleaseGivenOperationOfAnotherOwnerOrNoLogicalId` |
| Re-pin and no-op of same holder | `ShouldPinLaterGenerationGivenHolderClaimsAgain` |
| Superseded generation refused | `ShouldRefuseWithoutFactGivenHolderClaimsEarlierGeneration` |
| Taken name refused | `ShouldRefuseWithoutFactGivenAnotherProcessHoldsTheName` |
| Release and reuse | `ShouldFreeNameForAnotherProcessGivenExactHolderAndGenerationRelease` |
| Release refusals | `ShouldRefuseWithoutFactGivenReleaseOfOtherHolderOtherGenerationOrFreeName` |
| Exact retry | `ShouldKeepOneFactGivenExactClaimRetry` |
| Retry after later facts | `ShouldKeepOriginalResultGivenClaimRetryAfterRelease`, `ShouldKeepOriginalResultGivenReleaseRetryAfterLaterClaim` |
| Changed-intent refusal | `ShouldRefuseWithoutFactGivenLogicalIdReusedWithChangedIntent` |
| Replay of valid history | `ShouldRestoreHolderAndGenerationGivenValidHistoryReplay` |
| Competing decisions on one history | `ShouldRefuseCompetingProcessGivenHydratedHistoryHoldsTheName` |

Stale-revision append races are decided by the stream store against the loaded revision; the aggregate re-decides on reload, which the competing-decision test exercises. No malformed-history test was added (user preference).

## Event contract status

`OperationalProcessNameClaimed` and `OperationalProcessNameReleased` (discriminator version 1) match the catalog. Payload is owner, record revision, generation, intent reference, optional source proofs, operation provenance and the optional response and result references, shared with the other named-record claims. No payload duplicates stream identity. No event change is needed from this round.

## Later-stage follow-ups (not aggregate gaps)

- `ClaimOperationalProcessNameHandler` and `ReleaseOperationalProcessNameHandler` already decide replay through `Acceptance.Decide` ahead of the aggregate; confirm they keep that ordering and treat the aggregate's Conflict for a changed binding as permanent.
- `OperationalProcessNameClaimReactor` and `OperationalProcessOldNameReleaseReactor` own normalization, the active/kind policy, canonical acquisition order and the cancel fence.
- No projector is needed for the invariant.

## Evidence

- Prior focused review: Jev 1.14.0 assigned 0.74 probability that the owner-specific holder checks belong in this aggregate (advisory); the [request](jev-review-2026-10-27-operational-process-name-claim/request.json) and [response](jev-review-2026-10-27-operational-process-name-claim/response.json) are retained. The earlier run of the two original tests passed 2/2.
- This round: 16 new tests plus the 2 retained, covering the table above. **Written, not executed; build and run pending.** No Jev call was made this round; the retry gap is decided from the code and the catalog replay rule ("exact authorized retries resolve the original accepted result; changed intent is a permanent conflict").
- Status: the aggregate is complete for identity, invariant, lifecycle, event contracts and the focused tests once the pending build and test run is green. Full catalog and choreography conformance remains unqualified.

## Review before build (2026-10-29)

Independent review of the source, tests and this file by reading only; nothing was built or run.

### Findings

- No correctness gap found. Every accepted claim or release passes owner-type and tenant validation, the own-operation check, and the free/pin/exact-holder rule; an exact retry of an accepted logical id returns success with no second fact (the acceptance reducer would otherwise treat a second fact as corruption), and any changed field, fact type or command kind is a non-retryable Conflict. No payload field repeats stream identity.
- Tests assert events appended or not and state after replay; the retry tests would fail without `DecideRetry` (a second fact with the same logical id throws in `Acceptance.Observe`). Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Fixture values (tenant ids, `operational-process-<uuid>` streams from `OperationalProcess.Stream`, `inventory-registers` area) are valid for the types that validate them.
- Compile read: `Result?` with `is { } decided`, the `TFact : INamedRecordFact` pattern match, `Acceptance.Decide`, `AcceptedOperationBinding.Of`, `InventoryRegisters.Area` (same namespace) and the test helpers (`NativeFacts.Raised/Replay`, `ActorReference.ForSystemProcess`, `OperationProvenance` positional constructor) were checked against their declarations. No edits were needed.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/operationalprocessnameclaim-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer |
| --- | --- |
| Retry-after-release test asserts the invariant | Noul 0.89 |
| Changed-intent test proves the permanent-conflict rule | Noul 0.91 |
| Retry behavior consistent with the rule text | Noul 0.90 |
| Command-kind validation disposition (add_to_model / handler_concern / needs_source_resolution) | handler_concern 0.98, confidence 0.97 |
| Premise: model silent and code does not validate command kind | Noul 0.84 |

Discrepancy resolved: the aggregate does not validate `operation.CommandKind`; decisive disposition is handler concern, so no code change. Kind is still bound on retries through `AcceptedOperationBinding` (covered by the changed-kind case).

### Remaining risks

- Unexecuted: `AggregateScenario.PendingEvents` after `NativeFacts.Replay` is assumed empty, and the JSON round trip in `NativeFacts.Raised` is assumed to keep record equality for the intent and addresses (the sibling name-claim tests rely on the same).
- The constructor cannot re-derive the resource from the name; admission is the reactor's job.
- Build and test pending.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
