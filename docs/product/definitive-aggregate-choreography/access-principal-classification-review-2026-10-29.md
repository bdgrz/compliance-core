# AccessPrincipalClassification aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep (selected split from AccessPopulation).** One principal's classification history (`AccessPrincipalClassification`, stream `access-principal-classifications/{identity}` under the tenant) decides atomically that decisions are a contiguous per-principal sequence, so two principals never contend.

- Identity: `AggregateIdentity.Create("AccessPrincipalClassification", tenantId, populationId, providerSubjectId)`; the subject id is exact and ordinal. The `CommandId` is the logical operation id, never a stream key.
- Invariant: independent classification decision history for one accepted principal; each decision is bound to the exact expected revision (count of this principal's decisions). Human/nhi/shared/unclassified correlation rules are decided by the caller (complete decision is verified immutable content) and exact accepted-population proof is a coordinated read by the handler.
- Exclusions: the accepted raw population (`AccessPopulation`, never changed by classification), the principal list and history views (projections), effective access.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| any, revision n | Classify, valid own root, expected = n | revision n + 1 | `AccessPrincipalClassified` |
| any | Classify, stale or ahead revision | conflict | none |
| any | Classify, foreign stream, no expected revision, wrong command kind | validation refusal | none |
| any | Classify with blank subject or empty tenant or population | validation refusal | none |
| any | exact retry of a committed operation, including after later decisions | original result via `NativeAcceptance` (handler path) | none |

There is no terminal state; reclassification is a new decision at the next revision.

## Changes (real gap)

- `Classify` checked only owner stream and presence of an expected revision. An operation with a wrong command kind (or otherwise not an own root) was raised and then threw in `Apply`, instead of refusing without a fact. It now validates `NativeOwnerHistory.IsOwnRoot(..., nameof(ClassifyAccessPrincipal), expected)` before raising, refuses a blank subject or empty tenant/population, and rejects null response refs up front. The handler already supplies conforming values, so wire behavior is unchanged.

## Test-to-criterion table

New owner-level file `AccessPrincipalClassificationAggregateTests`; existing end-to-end tests are retained.

| Criterion | Test |
|---|---|
| Stable identity, exact ordinal subject | `ShouldKeepStreamOfPrincipalGivenTenantPopulationAndExactSubjectOnly` |
| Contiguous revisions, payload facts | `ShouldRaiseContiguousDecisionsGivenMatchingExpectedRevisions` |
| Stale revision appends nothing | `ShouldAppendNothingGivenStaleExpectedRevision` |
| Invalid decisions append nothing | `ShouldAppendNothingGivenOperationNotRootedInOwnPrincipalOrWrongCommand`, `ShouldAppendNothingGivenBlankSubjectOrMissingPopulation` |
| Replay of valid history | `ShouldRestoreRevisionAndLatestGivenValidDecisionHistory` |
| Competing decisions | `ShouldRefuseCompetingDecisionGivenHistoryAlreadyAdvancedPastExpectedRevision` |
| Exact retry, changed intent, retry after later decisions | existing `AccessPopulationIdentifiedOperationTests`, `ClassificationPreparationReplayTests` |
| Bounded history, source failure | existing `AccessPrincipalClassificationCompactionTests` |

Malformed-history tests were not added, by user preference.

## Event contract status

`AccessPrincipalClassified` carries tenant, population, subject, decision content ref, operation provenance and response refs; no stream metadata duplication. No add, rename, remove or payload change requested. Catalog still says discriminator version 1 while code declares 2 (already reconciled 2026-10-29 as the adopted wire).

## Later-stage follow-ups

- Handler: coordinated accepted-population proof and human/nhi/shared/unclassified rule check remain in `ClassifyAccessPrincipalHandler`.
- Projectors: principal list and history composition; review-exception surfacing of unclassified accounts.

## Evidence

Tests written, not executed; build and run pending. RED not observed. Status: done pending build.

## Review before build

Reviewer pass, written not executed (nothing compiled or run).

### Findings

- Implementation: `Classify` enforces the invariant before raising (own-root via `NativeOwnerHistory.IsOwnRoot`, expected revision, non-blank subject, non-empty tenant and population, non-null refs); `Apply` re-checks the same and throws only on malformed history. Competing and stale decisions are refused with Conflict and append nothing. Retry of a committed operation is acknowledged by `NativeAcceptance` in the handler path before `Classify`; the aggregate itself never double-appends. No payload field duplicates metadata (tenant/population/subject are identity key parts, existing wire).
- Behavior note: the handler admits any non-empty `ProviderSubjectId`; the aggregate now refuses a whitespace-only subject. Such a subject cannot be in an accepted population, so this is considered safe, but it is a narrowing.
- Test fix: `ShouldRaiseContiguousDecisionsGivenMatchingExpectedRevisions` did not prove contiguity per decision (Jev 0.75). It now captures the revision after the first decision (1) and asserts both pending events are distinct `AccessPrincipalClassified` facts.
- Compile review: `IsOwnRoot` takes `ulong?` and receives the pattern variable `expected` (ulong), valid. Test usings and `Assert.Equal(n, Pending(..))` (int-returning helper, not xUnit2013) look valid. Not confirmed: `AggregateScenario.PendingEvents` indexer/Count, and that the base `Aggregate` ctor accepts `Uuid.Empty` population and a blank subject (assumed from `AggregateIdentity.Encode`).

### Jev (jev-1.14.0, advisory), request/response in `implementation-jev/accessprincipalclassification-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability |
|---|---|---|
| contiguous test asserts rule | true | 0.75 (test then strengthened) |
| replay competing test asserts rule | true | 0.93 |
| non-own-root/wrong command refusal consistent | consistent | 1.00 |
| catalog discrepancy | no_discrepancy | 0.63 (add_to_model 0.20, needs_source_resolution 0.13); near-even-ish, unresolved |

Discrepancy: catalog discriminator version 1 vs code version 2 for `AccessPrincipalClassified`. Disposition: previously reconciled as adopted wire; Jev confidence 0.51 so left flagged, no event change, catalog edit (shared file) belongs to the coordinator.

### Remaining risks

Build and tests not run. Catalog version note above.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
