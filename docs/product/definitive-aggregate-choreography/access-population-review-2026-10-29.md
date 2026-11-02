# AccessPopulation aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain), with classification already split out.** One population (`AccessPopulation`, stream `access-populations/{populationId}` under the tenant) decides atomically that a stable population id binds one exact system-instance revision, observation and source, has draft facts replaced only while open, and is frozen exactly once by acceptance.

- Identity: tenant + caller/receiver-supplied immutable `PopulationId` (the logical id of the opening operation). Later mutations use `CommandId` bound to the population and expected revision. No caller key forks the stream.
- Invariant: immutable accepted population definition for the exact system instance and source revision; after acceptance the raw facts and snapshot binding never change.
- Exclusions: principal classification (`AccessPrincipalClassification`, separate owner), effective-access derivation (a derived calculation, never written as a source fact), snapshot content (`PopulationSnapshot`), missing-population exceptions (`AccessPopulationException`), system-instance visibility/revision currency (handler), fact-graph validation (`AccessPopulationAnalysis`), future-dated observation, authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Open (valid, rooted in own id) | opened, revision 1 | `AccessPopulationOpened` |
| none | Open (other logical id/stream, empty application/instance, blank source) | validation refusal | none |
| none | Record / Accept | not found | none |
| draft | Open again | conflict (exact retry resolves via native acceptance before this guard) | none |
| draft | Record, expected revision = current | revision + 1 | `AccessPopulationFactsRecorded` |
| draft | Record, stale revision | conflict (stale) | none |
| draft | Record without expected revision / foreign owner | validation refusal | none |
| draft, no facts | Accept | validation refusal | none |
| draft, facts | Accept, stale revision | conflict | none |
| draft, facts | Accept, blank attestation/hash or empty snapshot/calculation id | validation refusal | none |
| draft, facts | Accept, valid | accepted, revision + 1 | `AccessPopulationAccepted` |
| accepted | Record / Accept (new intent) | conflict, immutable | none |
| any | exact retry of a committed operation, including after later facts | original result via `NativeAcceptance` (handler path) | none |

## Changes (real gaps)

- Accepting a draft with no recorded facts was admitted by the aggregate; only the handler's graph validation stopped it. `PrepareAccept` now refuses (validation) when no facts content is staged, after the NotFound, accepted and stale guards. The handler's pre-check and in-owner prepare share this guard.
- `Open` accepted an empty application id, empty system-instance id and blank source; `Accept` accepted an empty snapshot or calculation id, blank content hash and blank attestation. These are now validation refusals that append nothing. The handler already supplies real values, so behavior through the API is unchanged.

## Test-to-criterion table

New owner-level file `AccessPopulationAggregateTests`; existing end-to-end operation tests are retained.

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks stream | `ShouldKeepStreamOfPopulationGivenTenantAndPopulationIdsOnly` |
| Valid open, payload facts | `ShouldRaiseRevisionOneOpeningGivenValidRootOperation` |
| Invalid decisions append nothing (open) | `ShouldAppendNothingGivenOpeningNotRootedInPopulationOrMissingBusinessFacts` |
| Second open refusal | `ShouldRefuseSecondOpeningGivenPopulationAlreadyOpened` |
| Facts replacement, contiguous revisions | `ShouldReplaceFactsAtNextRevisionGivenDraftAndMatchingExpectedRevision` |
| Refusal with no fact, stale revision | `ShouldAppendNothingGivenFactsForUnopenedStaleOrUnrootedRequest` |
| Accept needs facts | `ShouldRefuseAcceptanceGivenDraftWithoutRecordedFacts` |
| Accept refusals append nothing | `ShouldAppendNothingGivenAcceptanceMissingNotFoundStaleOrIncompleteSnapshot` |
| Frozen after acceptance | `ShouldFreezePopulationGivenAcceptanceAndRefuseFurtherChanges` |
| Replay of valid history | `ShouldRestorePopulationStateGivenValidOpenedRecordedAndAcceptedHistory` |
| Competing decisions after replay | `ShouldRefuseCompetingReplacementGivenHistoryAlreadyAdvancedPastExpectedRevision` |
| Exact retry, changed intent, retry after later facts, concurrent accept | existing `AccessPopulationIdentifiedOperationTests` (open, facts, accept, classify) |
| Bounded history / large raw graph | `ShouldAcceptCompleteRawGraphGivenMoreFactsThanTheFormerCap` (facts held as immutable content) |

Malformed-history tests were not added, by user preference.

## Event contract status

- `AccessPopulationOpened`, `AccessPopulationFactsRecorded` and `AccessPopulationAccepted` carry business facts plus native operation provenance; none duplicates stream metadata. No add, rename, remove or payload change requested.
- Discrepancy for the shared schema owner: the catalog lists discriminator version 1 for all three events, while the code declares `[Discriminator(..., 2)]`. Not changed here.

## Later-stage follow-ups (not aggregate gaps)

- Handlers keep owning system-instance currency, retired-instance refusal, future-dated observation, source and attestation length bounds, fact-graph validation and authorization precedence before replay.
- Catalog status `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED` and the `RecordAccessPopulationFacts` route shape (`record-access-population-facts/{command_id}`) are HTTP/handler stage items.
- Classification sequencing and exact accepted-population proof belong to `AccessPrincipalClassification`.

## Evidence

- Jev: no new request this round; decisions follow the catalog invariant and the skill's done criteria.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessPopulation"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: `NativeFacts.Root` provenance and `Raised` round-trip behavior are assumed from the AccessExpectation tests; any handler test accepting an empty draft now sees the aggregate's validation message instead of the handler's issue message (same error kind).

## Review before build (independent, 2026-10-29)

Source and tests were read, not trusted from the implementer summary. Nothing was compiled or run.

### Findings and fixes

- Implementation: the three added guards (open business facts, acceptance requires recorded facts, acceptance snapshot/hash/calculation/attestation) match the invariant. All refuse before `RaiseEvent`, so nothing is appended. Exact retries are decided by `NativeAcceptance` in the handler before `PrepareAccept` runs, so a committed accept is not refused by the new facts guard. No payload field duplicates metadata beyond tenant and population ids, which already exist as event identity in the shipped wire; no change requested.
- Handler impact checked: `AcceptAccessPopulationHandler` previously refused an empty draft anyway (`no_principals` issue, validation), so only the message differs ("Record the observed facts before acceptance."). The only test asserting the "issue" message records facts first, so it is unaffected. No shared test helper opens or accepts with empty ids.
- Tests: added coverage the first pass lacked: open with empty system-instance id, accept with blank content hash, accept with empty calculation id (all validation, nothing appended). Every other criterion already had a behavioral assertion (events pending or not, status, replayed state).
- Stale implementer item corrected: the catalog now records the adopted wire as the discriminator declared in code (2026-10-29 reconciliation), so the "catalog version 1 vs code 2" discrepancy no longer exists.

### Jev (model jev-1.14.0; advisory; request/response in `implementation-jev/accesspopulation-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Freeze test proves refused replacement and second acceptance append nothing | true | 0.92 |
| Replay competing test proves stale replacement refused, current appended once | true | 0.92 |
| Refusing accept of a draft with no facts consistent with rule | consistent | 0.98 (confidence 0.97) |
| Catalog/event discrepancy needing action | no_discrepancy | 0.96 (confidence 0.95) |

All decisive; no discrepancy left open.

### Remaining risks

- Not compiled: `NativeFacts.Root/Raised/Replay`, `AggregateScenario` use mirrors AccessExpectation tests; `Uuid?` optional parameter assumes `Uuid` is a struct (consistent with `default`/`== Uuid.Empty` usage).
- Unconfirmed at runtime: the replay test asserts zero pending after hydration.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
