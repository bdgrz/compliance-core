# AuditPopulation aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One population (`AuditPopulation`, stream `audit-populations/{populationId}` under the tenant) owns complete current definition, source and row classification, reconciliation invalidation, completeness proof and freeze seal as one conflict set (decision `OWNER_AuditPopulationRowDecision`). Row and version bodies are immutable content values outside the command owner; the owner retains only the compact `AuditPopulationHeader` and the current content ref.

- Identity: tenant + `PopulationId`, which equals the `DefinitionId` of the defining command. A request key (`CommandId`, `GenerationKey`) selects the native operation, never the stream: `Acceptance`/`SelectedCommandId` is not part of `EventStreamAddress`.
- Invariant: a manually recorded population cannot claim audit-ready or freeze until scope, observation time and completeness are declared; late or corrected rows never edit a frozen version but produce a successor version referencing the superseded one.
- Exclusions: period close snapshot authority (period owner), source fact resolution, export (`AuditPopulationExportProcess`), sampling, listing and projections of rows and counts.
- Bounded history: the aggregate holds no lifetime collection (verified by the existing capacity and compact-owner tests).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Define (exact current frozen close) | revision 1 | `AuditPopulationDefinedV4` |
| defined | Generate (generation key) | next revision | `AuditPopulationGeneratedV4` |
| defined | Revise definition / Record facts / Classify row / Reconcile | next revision | `...DefinitionRevisedV2`, `...FactRecordedV3`, `...RowClassifiedV3`, `...ReconciledV3` |
| reconciled | Freeze | frozen | `AuditPopulationFrozenV3` |
| frozen | Amend | successor version | `AuditPopulationAmendedV3` |
| any | stale or finalized-cut mismatch, foreign operation, missing receipt | conflict | none |
| any | exact retry of a committed operation | original Unit result | none |
| any | same key, changed intent or actor | conflict | none |

Rule-level refusals (unreconciled amend, stale revision, unauthorized fields) are decided by `AuditPopulationRules` before `Append`.

## Changes

No production change. Review against the catalog and source found no real gap: `Append` refuses (no `RaiseEvent`) on a non-current-content fact, wrong revision, oversize payload, wrong cut, foreign logical id, repeated selected fact or missing receipt; replay (`ObserveCurrent`, `ObserveHeader`) pins tenant, program, subject, header identity and revision.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no key fork | `ShouldDeriveStreamFromTenantAndPopulationGivenDifferentSelectedCommands` (new) |
| Invalid decision appends nothing | `ShouldRefuseAndAppendNothingGivenLegacyFactWithoutCurrentContent` (new), `ShouldRejectStalePopulationRevisionGivenSourceFacts` |
| Exact retry after later facts | `ShouldReplayAcceptedOriginalUnitWithoutAppendGivenAuditPopulationNativeBoundary`, `ShouldReplayOriginalUnitAfterClockAdvancesGivenAuditPopulationRootNativeBoundary` |
| Changed intent | `ShouldRejectChangedDefinitionGivenPreviouslyAcceptedDefinitionId`, `...DifferentActor...`, `...ChangedClose...` |
| Replay of valid history | `ShouldReplayExistingV3DefinitionGivenReceiptBearingDefinitionSupport`, `...V3Generation...` |
| Competing appends | `ShouldCommitOneDefinitionGivenConcurrentIdenticalRequests`, `ShouldCommitOneGenerationGivenConcurrentIdenticalRequests`, `ShouldRefuseCapturedTargetAfterRealSuccessorGivenAuditPopulationNativeResponsePreparation` |
| Bounded history | `ShouldKeepCompleteDefinitionsOutsideCommandOwnerGivenAuditPopulationNativeBoundary`, `ShouldRetainAllSeventyDefinitionsWithoutLifetimeCapGivenAuditPopulationNativeCapacity` |
| Freeze / amend lifecycle | `ShouldFreezeExactReconciledPopulationGivenRationale`, `ShouldRejectPopulationAmendmentGivenReconciledPopulationBeforeFreeze`, `ShouldFreezeCompletePopulationAndCreateImmutableSuccessorGivenLateCorrection` |

Malformed-history tests were not added, by user preference.

## Event contract status

V3/V4 facts carry the compact header, content refs and operation provenance; legacy facts remain replay-only. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: none identified.
- Projectors: row and count read models stay projections (no projected-count freeze authority).
- Export reactor/process remains `AuditPopulationExportProcess`.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test ... --filter "FullyQualifiedName~AuditPopulation"`, then the unit suite.
- RED: none captured; not claimed. The two new tests cover behavior already present in source.
- Residual risk: blind-written test may need `Stream`/`Id` member adjustments if `EventStreamAddress` lacks value equality.

## Review before build

Reviewer pass (written, not executed; nothing built or run).

Findings:
- The two new tests in `AuditPopulationAggregateTests` assert behavior (stream identity independent of the selected command id; Append of a legacy fact returns Conflict, appends nothing and leaves revision 0). Names and phases follow CNTRYL0001/0002.
- Compile read: `AuditPopulationPreparedCut` and `Append` are internal and visible to the test assembly; `ReplayCut.Require` succeeds for a fresh owner (committed position 0), so the refusal comes from the non-current-fact branch; `AuditPopulationFrozen(tenant, subject, revision, actorMemberId?, State, OccurredAt)` matches the six arguments; `null!` for State is legal at compile time and is never read because the fact is refused before `ObserveLegacy`. Residual unconfirmed: `EventStreamAddress` value equality (the existing tests also compare `.Stream`).
- Implementation: no gap. Refusals precede `RaiseEvent`; replay pins tenant, program, subject, header and revision; no payload field duplicates stream identity beyond the catalog event contract. No change made.
- Not covered by the new test: stale-cut and wrong-revision refusal; they are covered by the existing native admission and root admission tests.

Jev (model jev-1.14.0; request/response in `implementation-jev/auditpopulation-test-impl-review-2026-10-29-*.json`):
- Legacy fact on a new append: refuse_no_event 1.00.
- Identity test asserts the stable-stream rule: noul 0.90.
- Refusal test asserts a real no-fact outcome: noul 0.87.
- Late correction on frozen version: successor 1.00.

Discrepancies: none (no event or command in code lacks the catalog, or the reverse). Remaining: build and run pending.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
