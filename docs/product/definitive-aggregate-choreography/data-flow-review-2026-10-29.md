# DataFlow aggregate review

Verified against the polished 2026-10-29 `aggregates.json` DataFlow entry (`RecordDataFlow`, `ReviseDataFlow`; baseline event `DataFlowRevisionRecorded`). Earlier focused review (2026-10-27, Jev 1.14.0, 0.90 that DataFlow is an independent business owner) is recorded in `aggregate-backlog.md` and not repeated here.

## Identity, invariant, boundary

- **Identity:** one stream per tenant-scoped `DataFlowId` (`TechnologyInventoryStreams.Address(tenantId, id)`), shared with component and asset records so one identity holds one record kind. `RecordDataFlow.DataFlowId` is the caller-supplied immutable creation identity; an occupied identity is refused, never forked. `ReviseDataFlow` is keyed by `CommandId` (the operation logical id); the expected revision is a concurrency precondition, not identity.
- **Named invariant:** one flow decides atomically its endpoint and carried-asset scope, encryption-or-approved-exception terms for the carried classification, and a nondecreasing effective date, over a linear revision sequence.
- **Exclusions:** asset identity, classification and lifecycle belong to `InformationAsset`; the highest-carried classification and reference existence are resolved by the handler from asset projections before the aggregate is asked to decide; authorization, retry lookup by logical id and response freezing are handler work.
- **Boundary decision:** keep (retain). Complete carried-asset relation is evidence for one atomic encryption rule; no reason for all flows to share a revision; no reactor needed.
- **History bound:** the owner retains only the current content and revision. The asset list is complete and deliberately unbounded (150 assets are carried by an existing handler-level test).

## Lifecycle

| State | Command | Result |
| --- | --- | --- |
| No record | record valid | `DataFlowRevisionRecorded` revision 1, lifecycle forced `active` |
| No record | record invalid | Validation, no fact |
| No record | revise | missing-record refusal, no fact |
| Recorded | record again | Conflict, no fact |
| Recorded | revise at current revision, valid | revision + 1 |
| Recorded | revise at stale revision | Conflict (stale), no fact |
| Recorded | revise invalid, earlier effective date, or sensitive and unprotected | refused, no fact |
| Foreign kind on the identity | record or revise | Conflict / missing, no fact |
| Any | operation with empty logical id or another owner's stream | Validation, no fact |

Retire is a revision with lifecycle `retired`; distinct lifecycle facts stay under the open catalog decision `lifecycle_fact__DataFlow` (Jev noul 0.60, unsettled).

## Changes

- Source: the `DataFlowRevisionRecorded` reducer now refuses a fact whose tenant or flow id differs from the owner (same defence `Location` has), so a mismatched fact cannot silently hydrate. No behavior change for valid history. No test was added for it (malformed-history tests are excluded by preference).
- Tests: new `DataFlowTests` (written, not executed). Existing `TechnologyInventoryAggregateTests` and `InventoryRetryTests` keep encryption, effective-date, handler retry and unbounded-asset coverage.

## Test-to-criterion table

| Criterion | Test |
| --- | --- |
| Creation fact, normalization, forced active | `ShouldRecordRevisionOneWithNormalizedActiveContentGivenValidCreation` |
| Invalid creation appends nothing | `ShouldAppendNothingGivenInvalidCreation` |
| Occupied identity not re-created | `ShouldRefuseCreationAndAppendNothingGivenExistingFlow` |
| Shared-identity kind exclusion | `ShouldRefuseCreationAndAppendNothingGivenForeignInventoryStream` |
| Operation owner / logical id | `ShouldRefuseOperationAndAppendNothingGivenAnotherOwnerStream`, `ShouldRefuseOperationAndAppendNothingGivenEmptyLogicalId` |
| Revision without record | `ShouldRefuseRevisionAndAppendNothingGivenUnrecordedFlow` |
| Stale revision / competing decision | `ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedFlow` |
| Effective date never moves back | `ShouldRefuseRevisionAndAppendNothingGivenEarlierEffectiveDate` |
| Encryption or exception on revision | `ShouldRefuseRevisionAndAppendNothingGivenUnprotectedSensitiveFlow` |
| Encryption rule on creation | `ShouldRequireEncryptionOrExceptionGivenSensitiveFlow` (existing) |
| Valid replay restores state, next decision works | `ShouldRestoreRevisionAndContentGivenValidHistoryReplay` |
| Original acceptance after later facts (exact retry basis) | `ShouldResolveOriginalAcceptanceGivenReplayAfterLaterFacts` |
| Unbounded asset list survives replay in order | `ShouldCarryEveryAssetInOrderGivenLargeAssetListReplay` |
| Handler-level retry / changed intent | `InventoryRetryTests` (existing) |

## Event contract status

`DataFlowRevisionRecorded` (`bdgrz.inventory.data_flow.revision_recorded` v1) carries tenant, flow id, revision, complete content, actor, time, operation and first response. No rename, addition or payload change requested. The catalog's open `lifecycle_fact__DataFlow` decision is unchanged.

## Later-stage follow-ups (not aggregate gaps)

- `RecordDataFlowHandler` / `ReviseDataFlowHandler`: select by logical id and compare the `AcceptedOperationBinding` for exact retry versus changed intent.
- Handler resolution of carried classification from asset facts, and re-evaluation when an asset's classification rises (reactor or projection decision, not an aggregate check).

## Evidence

Tests are written, not executed; build and run pending (no build, test or format was run). No Jev call was made.

## Review before build

Independent review of the source and `DataFlowTests` before any build. Nothing was built, run or formatted.

### Findings and fixes

- **Test bug (fixed):** `ShouldRestoreRevisionAndContentGivenValidHistoryReplay` compared `source.Content` and `replayed.Content` with `Assert.Equal`. `DataFlowContent` is a record holding `IReadOnlyList<Uuid>`, so record equality compares the list by reference, and the JSON round trip makes a new list; the assertion would always fail. It now uses `TechnologyInventoryRules.SameContent`.
- **Compile risk (fixed):** removed three unused `using` lines (`Evidence.Models`, `NamedRecords.Contracts`, `NamedRecords.Models`) that no existing inventory test imports and that were not needed.
- **Weak assertions (fixed):** the unrecorded-flow, earlier-date and unprotected-sensitive tests asserted only `IsSuccess == false`. They now assert `NotFound`, `Validation` and `Validation`; the unprotected test also asserts the flow still reports at-rest encryption.
- **Implementation:** no gap found. Stale revision, foreign kind, occupied identity, earlier date, encryption-or-exception, wrong-owner or empty-logical-id operations all refuse before `RaiseEvent`. The tenant and flow-id reducer guard is correct. Retry of a committed fact is acknowledged by the handler via `Acceptance`, which the replay test covers.

### Jev (jev-1.14.0; `implementation-jev/dataflow-test-impl-review-2026-10-29-*.json`, advisory)

| Question | Answer |
| --- | --- |
| Stale-revision test asserts the invariant | Noul 0.85 |
| Earlier-date test asserts the invariant | Noul 0.85 |
| Encryption-or-exception test asserts the invariant | Noul 0.90 |
| TenantId/DataFlowId in payload | keep_as_cataloged 0.91 (remove 0.07, needs source 0.02); premise Noul 0.94 |
| Identical revision at current revision | accept_new_revision 1.00 |

Dispositions: payload fields stay as cataloged, no event change needed; identical revision stays accepted. No discrepancy between code and catalog was found. `lifecycle_fact__DataFlow` (0.60) stays open and unresolved.

### Remaining risks

- Not compiled or run. Unconfirmed: that `Assert.Equal(Uuid[], IReadOnlyList<Uuid>)` binds to the sequence overload, and that unused usings are not an error under the repo analyzers (the existing `TechnologyInventoryAggregateTests` carries the same list).


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
