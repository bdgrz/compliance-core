# InformationAsset aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together; retained as its own owner** (catalog `boundaryDecision: retain`, group 14). One tenant-scoped information asset (stream `TechnologyInventoryStreams.Address(tenantId, informationAssetId)`) decides, atomically, that an identity is one information-asset record carrying exactly one classification (public, internal, confidential or restricted), a retention reference, an owner person and an active or retired lifecycle, at one revision. Name, classification, retention, owner and lifecycle are one governed record, so they stay together. A missing or second classification is refused because the value is a single validated string.

- Identity: tenant plus caller `InformationAssetId`. Creation uses `InformationAssetId` as the immutable creation logical identity (handler); revise uses `CommandId` plus the expected revision. The stream address derives from tenant and asset id only, so no request key can fork a stream. Shared inventory sentinels (`TechnologyComponentRevisionRecorded`, `DataFlowRevisionRecorded`) mark the identity as foreign, so the id cannot be recorded as another kind.
- Invariant: one tenant inventory identity has one record kind and one revisioned classification/retention/owner/lifecycle record.
- Exclusions: DataFlow owns carried-asset scope and encryption rules; the impact evaluator (`InformationAssetImpact`) is a pure read-side evaluation over flows. Owner-person existence is a handler reference check (`TechnologyInventoryReferences`), not an aggregate fact. Listings and directories are projections. TechnologyComponent is a separate owner on the shared address scheme.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record, valid | created at revision 1, lifecycle forced active | `InformationAssetRevisionRecorded` (revision 1) |
| none | Record, invalid content, foreign owner, empty logical id | validation refusal, no event | none |
| none | Revise | not found, no event | none |
| foreign-kind stream | Record | conflict, no event | none |
| foreign-kind stream | Revise | not found, no event | none |
| created | Record again (same or changed) | conflict at the aggregate; exact retry is decided earlier by native acceptance and replays the original registration | none |
| created | Revise, expected = revision, valid | revision + 1 (including retire) | `InformationAssetRevisionRecorded` |
| created | Revise, stale expected revision | conflict, no event | none |
| created | Revise, invalid content or foreign-owner or empty logical id | validation refusal, no event | none |

Lifecycle is active or retired. Reactivation is not prohibited by the catalog, so it is not invented. Distinct lifecycle facts are an open catalog item (`OPEN_LifecycleFactsOtherInventories`, InformationAsset 0.54); generic revision facts stay.

## Changes

No production change. Adversarial qualification found no behavioral defect in `InformationAsset` or `TechnologyInventoryRules`.

- Exact retry of a committed creation or revision after later facts is decided by `NativeAcceptance.Decide` over the fact carrying the selected logical id, never by current content. Handlers hydrate with the selected logical id.
- Changed intent under a used logical id yields `Conflict`; stale revision is decided on the single revision counter before content is evaluated.
- Invalid decisions raise nothing: `PrepareRecord` and `PrepareRevise` validate before `RaiseEvent`.
- The event payload carries business content plus the existing native operation provenance; no metadata is duplicated.
- History is bounded: one header-sized fact per revision, no embedded collections.

Coverage gaps closed (tests only, new file `InformationAssetTests.cs`). The existing aggregate tests covered only invalid classification or retention at creation and foreign-stream hydration.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, normalization, revision 1, lifecycle forced active | `ShouldRaiseNormalizedActiveRevisionOneGivenNewAsset` |
| Second creation refused, nothing appended | `ShouldRefuseCreationAndAppendNothingMoreGivenRecordedAsset` |
| Invalid content appends nothing (name, retention, owner, description) | `ShouldAppendNothingGivenInvalidCreationContent`; classification and blank retention in `ShouldRejectGivenInvalidClassificationOrRetention` (existing) |
| Length boundaries | `ShouldAcceptBoundaryLengthsGivenMaximumNameRetentionAndDescription` |
| Operation must be the asset's own | `ShouldRefuseCreationGivenOperationOfAnotherOwnerOrWithoutLogicalId`, `ShouldRefuseRevisionGivenOperationOfAnotherOwnerOrWithoutLogicalId` |
| Revision advance, reclassify, retire | `ShouldAdvanceRevisionAndRetireGivenRevisionAtCurrentRevision` |
| Competing decisions at one expected revision | `ShouldRefuseSecondRevisionGivenCompetingRevisionsAtSameExpectedRevision` |
| Missing record, invalid classification (single value only), retention, lifecycle, owner on revise | `ShouldAppendNothingGivenInvalidRevisionOrMissingAsset` |
| Valid replay restores revision and content; stale after replay | `ShouldRestoreRevisionAndContentGivenValidHistoryReplay` |
| Exact retry after later facts, changed intent, unaccepted operation | `ShouldDecideReplayedGivenSelectedOperationAfterLaterRevision`, `ShouldDecideNoneGivenUnacceptedLogicalOperation` |
| Foreign kind isolation | `ShouldNotHydrateAsAnotherKindGivenForeignInventoryStream` (existing), `ShouldNotReviseGivenForeignInventoryStream` |
| Handler-level exact retry end to end | `ShouldReplayOriginalRegistrationGivenExactAssetRetryAfterLaterRevision` (existing, `InventoryRetryTests`) |
| Read-side impact evaluation | `InformationAssetImpactTests` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

- `InformationAssetRevisionRecorded` (`bdgrz.inventory.information_asset.revision_recorded`, v1) carries tenant, asset id, revision, content, actor, changed-at, operation provenance and first response. It is the single fact for creation and revision.
- No payload, rename or discriminator change is needed. No event changes are requested.
- Open decision, unchanged: a revise whose content equals the current content still raises a revision. The catalog gives no no-op rule for revise (exact retries are handled by native acceptance), so none is invented.

## Later-stage follow-ups (not aggregate gaps)

- Handler: confirm `RecordInformationAsset` selects `InformationAssetId` as its logical id and `ReviseInformationAsset` selects `CommandId`; a revise `CommandId` equal to the asset id hydrates the creation fact and is decided Conflict, not a fork.
- Owner-person reference checks stay in the handler layer.
- Impact reactor: reclassification or retirement re-evaluating carrying data flows (`InformationAssetImpact`) is a reactor concern.
- Directory and revision-list projections remain projections of this stream.
- Distinct lifecycle facts remain an open catalog decision.

## Evidence

- Jev: no new request this round. The retained 2026-10-27 review (jev-1.14.0) returned Noul 0.80 that InformationAsset is an independent business owner ([request](jev-review-2026-10-27-three-b/request.json), [response](jev-review-2026-10-27-three-b/response.json)); the 2026-10-29 polish recorded 0.54 on distinct lifecycle facts, which stays open.
- Tests: written, not executed; build and run pending. Intended command: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~InformationAssetTests|FullyQualifiedName~TechnologyInventoryAggregateTests|FullyQualifiedName~InventoryRetryTests|FullyQualifiedName~InformationAssetImpactTests"`, then the backend unit suite.
- RED: none captured. The new tests cover behavior already implemented, so no failing run is expected; this is not a claimed red.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review of source `InformationAsset.cs` (unchanged), `InformationAssetTests.cs` (new, untracked) and the review file. Nothing was built or run.

### Findings

- Implementation: no defect. Every accepted decision validates before `RaiseEvent`; foreign-kind, second creation, stale expected revision, and foreign-owner or empty logical id all append nothing; exact retries are decided by `NativeAcceptance` on the selected logical id, not by current content; the payload carries no stream-identity duplication beyond the catalogued tenant and asset id.
- Tests: each asserts events appended or not and state after the decision; none is structure-only. The reactivation path (retired back to active) is exercised by the revise in `ShouldRestoreRevisionAndContentGivenValidHistoryReplay`. No criterion without a test. Names and Arrange/Act/Assert phases follow CNTRYL0001 and CNTRYL0002.
- Compile read-through: the 3-arg `Record` and 4-arg `Revise` calls bind to the `InventoryFacts` extension methods (`test/Compliance.Tests/InventoryFacts.cs`, namespace `Bdgrz.Compliance.Tests`, visible from the nested test namespace); `AssetFact`, `ComponentFact`, `DomainEventSeed.Attach`, `AggregateScenario.Given/PendingEvents/Aggregate`, `AcceptedOperationBinding.Of`, `NativeAcceptanceDecision` and `OperationProvenance` `with` expressions match existing usage in `TechnologyInventoryAggregateTests` and `DataFlowTests`. Fixture values are valid (64-character hashes, non-empty ids).
- No fixes were required.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/informationasset-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer |
|---|---|
| Revise with unchanged content: append revision / no-op / needs source | append_revision 1.00 |
| Reactivate retired asset: allow / refuse / needs source | allow 1.00 |
| Competing-revision test asserts the invariant (Noul) | 0.98 |
| Second creation Conflict consistent with rule (Noul) | 0.80 |
| Two-classification test asserts the rule (Noul) | 0.97 |

Disposition: the two open points from the implementer (unchanged-content revise, reactivation) are resolved decisively in favor of current behavior; no code change. No catalog, event, schema or name discrepancy exists, so nothing is added to eventChangesNeeded. Distinct lifecycle facts (catalog 0.54) remain an unresolved catalog item; it is not a correctness gap.

### Remaining risks

- Tests written, not executed; compile was read by eye only.
- Handler stage must confirm logical-id selection (`InformationAssetId` for creation, `CommandId` for revise).


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
