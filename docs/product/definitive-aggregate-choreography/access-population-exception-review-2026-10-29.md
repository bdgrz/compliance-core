# AccessPopulationException aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run pending.

## Boundary decision

**Keep as a single-fact independent owner (retain).** One exception (stream `access-population-exceptions/{exceptionId}` under the tenant realm) decides, once, that an approved missing-population exception exists for an exact system instance, with its reason and future expiry held as verified immutable content (`AccessPopulationExceptionView`).

- Identity: tenant + caller-supplied immutable `ExceptionId` (the logical id of the creating operation). The catalog's `(systemInstanceId, exceptionId)` is a baseline observation only; the selected summary and the shipped stream use the exception id. No request key forks the stream; a different id is a different record, so unrelated exceptions never contend (the split away from the shared system ledger).
- Invariant: an exception is recorded once, by its own root operation at expected revision 0, for a non-empty system instance; the recorded fact is immutable and there is no later mutation. Expiry is evaluated at authoritative read time and needs no fact.
- Exclusions: reason bounds, future-expiry check, system-instance existence and visibility, actor authorization and personal-HTTP requirement (handler); proposer/reviewer separation and the "exception references approved expectation" rule are catalog `mustPreserve` text that belongs to AccessExpectation and the review gate, not to this record; exception consumption (readers/projections).
- Alternatives: split further (no, one immutable fact); reactor (no, no cross-owner decision).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record, own root operation, expected revision 0, non-empty instance | recorded, revision 1 | `AccessPopulationExceptionRecorded` |
| none | Record, other logical id or other stream, no expected revision, empty system instance | validation refusal | none |
| none | Record, expected revision other than 0 | conflict | none |
| recorded | Record (new intent) | conflict, already exists | none |
| any | exact retry of the committed operation, including after later exceptions | original result via `NativeAcceptance` (handler path) | none |
| any | same id, changed intent | conflict via native acceptance | none |

No expiry, revoke or edit transition exists in the catalog; none was invented.

## Changes (real gaps)

- `Record` accepted an empty system-instance id and would append a fact bound to no instance. It now refuses with a validation error and appends nothing. The handler always supplies the request's instance (validated against a found instance), so API behavior is unchanged.
- `Record` now rejects a null first response up front (`ArgumentNullException`) instead of raising a fact that the replay guard in `Apply` would later refuse.

## Test-to-criterion table

New owner-level file `AccessPopulationExceptionTests`; the end-to-end operation tests in `AccessExpectationIdentifiedOperationTests` are retained.

| Criterion | Test |
|---|---|
| Stable identity, no key forks stream | `ShouldKeepStreamOfExceptionGivenTenantAndExceptionIdsOnly` |
| Valid creation, payload facts | `ShouldRecordRevisionOneExceptionGivenValidRootOperation` |
| Invalid decisions append nothing | `ShouldAppendNothingGivenRecordNotRootedInExceptionOrMissingBusinessFacts` |
| Second record refused, immutability | `ShouldRefuseSecondRecordGivenExceptionAlreadyRecorded` |
| No contention between unrelated exceptions | `ShouldNotContendGivenUnrelatedExceptionsInTheSameTenant` |
| Replay of valid history | `ShouldRestoreExceptionStateGivenValidRecordedHistory` |
| Competing decision after replay | `ShouldRefuseCompetingRecordGivenHistoryAlreadyRecorded` |
| Exact retry after others, changed intent, nonzero revision, expired refusal | existing `ShouldReturnOriginalExceptionGivenExactPopulationExceptionRetryAfterOthers`, `ShouldConflictGivenSamePopulationExceptionIdentityWithChangedIntentOrNonzeroRevision` |
| Bounded history | one fact per stream by construction |

Malformed-history tests were not added, by user preference.

## Event contract status

- `AccessPopulationExceptionRecorded` carries tenant, system instance, revision, content ref and native operation provenance; no stream metadata duplicated. No add, rename, remove or payload change requested.
- Catalog `aggregates.json` lists discriminator version 1 while code declares `[Discriminator("bdgrz.access_population.exception_recorded", 2)]`; the shared catalog owner should reconcile this as was done for AccessPopulation. Not changed here.
- Catalog `baselineSourcePath` and the inventory path (`.../AccessReviews/AccessPopulationException.cs`) are stale; the file is under `Aggregates/`. Shared files, not edited.

## Later-stage follow-ups (not aggregate gaps)

- Handler keeps reason bound, future-expiry check, instance visibility and personal HTTP authorization.
- Catalog status `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` and the PUT route shape belong to the handler/HTTP stage.
- Readers that filter expired exceptions at read time use the injected clock.

## Evidence

- Jev: no request this round; the aggregate is a single immutable fact and the decisions follow the catalog.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessPopulationException"`.
- RED: none captured; not claimed.

## Review before build

Independent review, 2026-10-29. Nothing compiled or run.

### Findings
- Source diff (null first-response guard, empty-instance validation) is correct: the handler always passes a non-null frozen response and a found instance, so API behavior is unchanged; refusals precede `RaiseEvent`, so nothing is appended on refusal. No payload field repeats the stream id (tenant and instance only).
- Tests: every test asserts events appended or not, or state after replay. Fixtures (`NativeFacts.Root/Ref/Raised/Replay`, `Uuid.Empty`, `AggregateScenario.PendingEvents.Count`) match the helper signatures and sibling `AccessPopulationAggregateTests`. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. History is built on one seed owner and `Raised` is called once. No `Attach` on pending events.
- Compile read: usings match the sibling file; `Assert.Single`, `Assert.IsType` used; no inline array constants; no nullable-error pattern (`Error?.Kind` on Result). No compile risk found. No edits were needed.
- Gap noted, not a defect: exact retry and changed-intent coverage stays at handler level (existing tests).

### Jev (model jev-1.14.0; files `implementation-jev/accesspopulationexception-test-impl-review-2026-10-29-*.json`)
| Question | Answer | Probability |
|---|---|---|
| Refusals test proves invalid decisions append nothing | true | 0.93 |
| Competing-record-after-replay test proves refusal and preserved fact | true | 0.92 |
| Empty instance Validation refusal consistent with rule | consistent | 1.00 |
| Discriminator version 1 vs 2 and stale path: discrepancy kind | no_discrepancy | 0.78 (needs_source_resolution 0.21) |

The catalog item is a shared-file reconciliation (decisive but not strong); no code change here. Left to the catalog owner: set `discriminatorVersion` to 2 and fix the stale `baselineSourcePath`/inventory path.

### Remaining risks
- Tests unexecuted; `Assert.Equal(1, recorded.Revision)` relies on int-to-long inference (same pattern as siblings).


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
