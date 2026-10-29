# ApplicationImportSource aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and views are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep.** One source (`ApplicationImportSource`, stream `application_import_sources/{IdFor}` under the tenant) serializes batch admission and source-record mapping publication for one exact tenant, source key and namespace.

- Identity: `IdFor(tenantId, sourceKey, sourceNamespace)`, length-prefixed UUIDv5; rejects empty tenant, blank, surrounding whitespace and values over 128 characters; no case folding or normalization. The command id is the logical operation id, never a stream key.
- Invariant: at most one batch holds the source; a batch is admitted only against the exact source revision it was frozen against and only if it has neither held nor been fenced; a batch settles at most once; only a batch that was admitted can settle as published; a batch settled before admission leaves a fence so a delayed claim is refused.
- Exclusions: source claims and mapping derivation (a projection of published batches, `ApplicationImportSourceView`), the batch lifecycle (`ApplicationImportBatch`), application identity, plan sealing and the admission reactor. No independent `ApplicationSourceClaim` root.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| idle, batch selected, never seen | Claim, valid lineage | holder = batch | `ApplicationImportSourceAdmissionRecorded` |
| idle, never seen | Settle canceled | fence | `ApplicationImportSourceSettled` |
| idle, never seen | Settle published | conflict | none |
| held by batch | Settle (published or canceled) | idle, slot settled | `ApplicationImportSourceSettled` |
| held by other batch | Claim | transient conflict | none |
| batch fenced or settled | Claim or Settle | conflict | none |
| any | wrong command kind, missing first owner or causation, unselected or empty batch, undefined disposition | refusal | none |
| any | exact retry of committed operation, including after later facts | original result via `NativeAcceptance` (handler path) | none |

Stale-plan fencing against the frozen source revision is enforced by the batch/reactor path with the expected source revision (existing end-to-end test `ShouldRefuseClaimGivenSourceChangedAfterPlanFroze`).

## Changes

No source change. Adversarial pass found no real aggregate gap: `Claim` and `Settle` validate lineage and guard state before `RaiseEvent`, `Apply` re-checks the same on replay, history is bounded (only the active and one selected slot are kept; test `ShouldKeepBoundedSourceAuthorityGivenManyBatchesSettledBeforeAdmission`).
The gap was coverage: the aggregate was only exercised end to end. New owner-level file `ApplicationImportSourceAggregateTests`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, exact key and namespace | `ShouldKeepStreamOfSourceGivenTenantExactKeyAndNamespaceOnly`, `ShouldRejectSourceIdentityGivenUnboundedOrUntrimmedKeys` |
| Admission, payload facts | `ShouldHoldSourceGivenSelectedBatchClaimOnIdleSource` |
| Invalid claims append nothing | `ShouldAppendNothingGivenClaimWithInvalidLineageOrUnselectedBatch` |
| Fence before admission | `ShouldFenceBatchGivenCanceledSettlementOfNeverAdmittedBatch`, `ShouldFenceLateClaimGivenSettlementBeforeAnyAdmission` |
| Invalid settlements append nothing | `ShouldAppendNothingGivenInvalidSettlementOfNeverAdmittedBatch` |
| Replay of valid history | `ShouldRestoreHolderGivenValidAdmissionHistory`, `ShouldKeepSelectedEarlierFenceGivenLaterFencedBatchAndActiveOtherHolder` |
| Competing decisions | `ShouldRefuseCompetingClaimGivenAnotherBatchHoldsSource`, `ShouldAdmitOneBatchAtATimeGivenSealedBatchesOfOneSource` |
| Already settled or fenced | `ShouldRefuseClaimAndSecondSettlementGivenBatchAlreadyFenced` |
| Exact retry | `ShouldAdmitOneBatchAtATimeGivenSealedBatchesOfOneSource` (Replayed outcome) |
| Stale source revision | `ShouldRefuseClaimGivenSourceChangedAfterPlanFroze` |
| Bounded history, native cut | `ShouldKeepBoundedSourceAuthorityGivenManyBatchesSettledBeforeAdmission`, `ApplicationImportSourceHistoryProofTests` |

Owner-level tests stay on the first appended fact of a fresh owner because later appends require the finalized native cut. Malformed-history tests were not added, by user preference.

## Event contract status

`ApplicationImportSourceAdmissionRecorded` and `ApplicationImportSourceSettled` carry owner, revision, generation, intent ref, operation provenance, batch id, source key/namespace (admission) and disposition (settle); no metadata duplication. No add, rename, remove or payload change requested.

## Later-stage follow-ups

- Reactor: `ImportSourceAdmissionReactor` plan-trigger verification (covered by existing forged-trigger test).
- Projectors/views: `ApplicationImportSourceView` claim derivation from published batches, mapping ambiguity refusal.

## Evidence

Tests written, not executed; build and run pending. RED not observed. Status: done pending build.

## Review before build

Independent review before any build; nothing compiled or run.

### Findings

- Implementation: `Claim` and `Settle` validate lineage and state before `RaiseEvent`; `ApplyFact` re-checks revision, generation, single holder, settle-once and published-only-after-admission. No stale, duplicate, competing or replayed input appends a wrong fact. A repeated claim by the holding batch is refused (conflict) rather than re-acknowledged; exact-retry acknowledgement is the handler's `NativeAcceptance` path, outside this stage. No payload field duplicates metadata.
- Tests: all eight new tests assert events appended or not, owner state or replayed state. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Fixtures: key `manual`, namespace `applications` are within 128 and trimmed; `NativeFacts.Ref()` is a valid content ref; replay histories are built on one seed owner and `Raised` is called once; state is asserted after refusals only, so no state-moves-on pitfall. `Pending` uses `.Count` on the list. No test edits were needed.
- Compile risk: read each changed line; usings cover `Uuid`/`Result`/`RequestErrorKind` (Cntryl.Portia), `AggregateScenario` (Portia.Testing), `NativeFacts` (Tests.Testing), `OperationProvenance` (NativeOperations.Models), commands, events and `ApplicationImportDisposition` (Applications.Models, assumed). Not confirmed without compiling: that `ApplicationImportDisposition` lives in `Applications.Models`; that `Assert.Equal(1, long)` infers `long`; that `OperationProvenance` is a record permitting `with { FirstOwner, CausationEventId }`. The `withoutFirstOwner` case may be refused because `RootOperation.Provenance` sets `FirstOwner` to the source stream (not the batch stream), which is still a lineage refusal.
- Coverage limit: owner-level history stays at one fact per owner because later appends need a finalized native cut; settle-after-admission by the same owner is covered only by existing end-to-end tests.

### Fixes

None required; no source or test change.

### Jev (jev-1.14.0, advisory; files in `implementation-jev/applicationimportsource-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Competing-claim test proves single holder, nothing appended | true | 0.89 |
| Fence test proves fenced batch cannot claim or settle again | true | 0.93 |
| Invalid-settle test proves nothing appended, never-admitted cannot publish | true | 0.88 |
| Catalog discrepancy: `preliminaryNewEvents` lists only `ApplicationImportSourceAdmissionRecorded` while `ApplicationImportSourceSettled` is registered via the release command and event record | no_discrepancy 0.50, add_to_model 0.41 | confidence 0.33, near-even: UNRESOLVED |

### Remaining

- Catalog bookkeeping: `aggregates.json` `preliminaryNewEvents` for ApplicationImportSource omits `ApplicationImportSourceSettled` although the release command and the event record list it. Jev is near-even, so it is flagged for the shared-file owner, not applied. No event add, rename or payload change is needed in code.
- Build and run pending; the compile items above are unconfirmed.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
