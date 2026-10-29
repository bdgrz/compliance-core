# OperationalProcess aggregate review

Updated 2026-10-29 (batch session, aggregate stage only). Tests are written, not executed; build and run pending.

## Identity, invariant and exclusions

- **Identity:** one stream per tenant-scoped operational process, `InventoryRegisters.OperationalProcessAddress(tenantId, operationalProcessId)`. `OperationalProcessId` is the caller-supplied immutable creation identity; an exact retry resolves the original result and a different id is a different record. Later mutations bind CommandId to this identity and the expected revision. No request key forks the stream.
- **Named invariant:** the independent governed lifecycle of one operational process: strictly sequential revisions (each change is accepted against the exact current revision), at most one pending change, and an apply or cancel only for the exact pending change.
- **Exclusions:** active-name uniqueness is decided by `OperationalProcessNameClaim`. Content validation, authorization and the immutable content store belong to the handler and framework. A written procedure is policy content, never an OperationalProcess record.

## Boundary decision

Keep the record content/version lifecycle in this owner. The name claim stays split: a tenant-wide uniqueness rule must not serialize unrelated processes, and the invariant is preserved by the claim aggregate's authoritative holder admission, never a projection. Rosters and histories are projections. History is bounded by one stream per process and one accept/apply pair per revision.

## Lifecycle

| State | Decision | Result |
| --- | --- | --- |
| none | accept creation | `OperationalProcessChangeAccepted` (revision 0, generation 1) |
| none | revise | refused NotFound, no fact |
| recorded | accept creation again | refused Conflict, no fact |
| recorded | accept revision at current revision | `OperationalProcessChangeAccepted` |
| recorded | accept revision at stale revision | refused stale revision, no fact |
| pending | accept another change | refused retryable Conflict, no fact |
| pending | apply exact change | `OperationalProcessRevisionRecorded` (revision + 1) |
| pending | apply with different intent or root | refused Conflict, pending kept |
| pending | cancel exact change | `OperationalProcessChangeCanceled`; later apply fenced |
| recorded, no pending | apply or cancel | refused Conflict, no fact |
| retired | terminal | see the retirement finding below |

## Retirement (EV-D07, planned `OperationalProcessRetired`)

Today retirement is only the `Lifecycle` value inside `OperationalProcessContent`, carried by an ordinary revision (the Location equivalent is exercised in `NamedRecordProcessTests`). The catalog (aggregate-details, events.md, choreography X004/T-reaction) adopts retirement as a distinct terminal fact so that the name-release reactor and consumers recognise it without reading content. This is a real lifecycle gap in the sense of the catalog, but it cannot be closed in this session: the event does not exist in `tools/domain-events/**`, the JSON context or the event catalog, and a retire command needs a source-backed decision on terminal refusal (no later revision, no reactivation) that depends on the new event.

No aggregate code was added for it (no speculative command or fact). Required change is recorded under event contract status and in the structured result. When the event exists, the aggregate adds a `Retire` decision (exact expected revision, no pending change, idempotent no-op refusal on an already retired stream, terminal guard in `CheckAccept`/replay) with tests written first.

## Changes

- New `OperationalProcessAggregateTests` covering the owner-level criteria that were previously covered only through the shared host process tests. No production change: the existing source already satisfies the invariant (tenant check in `ObserveOperationalProcessRevision`, shared `NamedRecord` guards).

## Test-to-criterion table

| Criterion | Test |
| --- | --- |
| Replay/hydration of valid history | `ShouldRestoreRevisionAndLifecycleGivenValidRecordedHistory` |
| Refusal appends no fact, one pending change | `ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange` |
| Stale revision | `ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedProcess` |
| Stable identity, no re-creation | `ShouldRefuseCreationAndAppendNothingGivenExistingIdentity` |
| Revision of unknown process | `ShouldRefuseRevisionAndAppendNothingGivenUnrecordedProcess` |
| Changed intent refusal | `ShouldRefuseApplyAndKeepPendingGivenChangedIntent` |
| Retry of a committed fact (aggregate level refusal; original result replay is `Acceptance` and the shared host test `ShouldReplayOriginalResponseGivenExactRetryAfterLaterChangeAndConflictOnChangedIntent`) | `ShouldRefuseSecondApplyAndAppendNothingGivenCommittedRevision` |
| Cancel fences later apply, delayed append | `ShouldFenceApplyAndClearPendingGivenCanceledChange` |
| Cancel with nothing pending is refused | `ShouldRefuseCancelAndAppendNothingGivenNoPendingChange` |
| Competing creation/rename of one name | existing `ShouldRenameAndRefuseDuplicateGivenOperationalProcesses`, `ShouldLetExactlyOneWinGivenTwoConcurrentCreationsOfOneName` |
| Tenant on revision fact | `OperationalProcess.ObserveOperationalProcessRevision` (no malformed-history test, per user preference) |

## Event contract status

| Event | Status |
| --- | --- |
| `OperationalProcessChangeAccepted` | exists; payload carries stream, expected revision, generation, intent |
| `OperationalProcessChangeCanceled` | exists |
| `OperationalProcessRevisionRecorded` | exists; carries tenant, id, revision, content ref, actor, time |
| `OperationalProcessRetired` | planned, not implemented; needs schema, discriminator, JSON context entry and catalog promotion by the coordinator |

Jev was not consulted in this session; the earlier Jev 1.14.0 tenant-check judgment (0.96, advisory) stands, with request and response retained in `jev-review-2026-10-27-operational-process/`.

## Later-stage follow-ups

- Handler for retire, and reactor X004 releasing the name on `OperationalProcessRetired` (releasing only for the exact holder and generation).
- Consumers (boundary/description content) refusing new citation of a retired process.

## Evidence

Tests are written, not executed; build and run pending. Nothing was built, formatted or run in this session.

## Review before build (2026-10-29)

Independent review of the batch implementation; nothing built or run.

### Findings and fixes

- **Weak test (fixed):** `ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedProcess` asserted only `IsSuccess == false` and a fact count; it now asserts `RequestErrorKind.Conflict` (from `VersionedRecordRules.StaleRevision(...).ToRequestError()`), no pending change, revision still 2, and no new fact.
- **Coverage gap (fixed):** nothing proved a new change is accepted after a cancel. Added `ShouldAcceptNextChangeAtNewGenerationGivenCanceledRevisionChange` (recorded at revision 1, accept, cancel, accept again at the same expected revision; generation 3, new root pending, old root not pending, 5 facts). A first draft used a canceled creation and would have failed: `PrepareChange` refuses a creation once `Generation > 0`, so a canceled creation leaves the identity consumed (revision 0, revision attempts NotFound). This is consistent with the catalog identity rule (a different id is a different record) and is recorded here as intended behavior, not tested.
- **Implementation:** no defect found. Stale, duplicate, pending and replayed inputs are refused by `NamedRecord` guards before any `RaiseEvent`; exact retries of committed facts are answered from `Acceptance`, not by new events; revision fact carries `TenantId` and `Id` as in the existing contract (pre-existing, not changed).
- **Compile read-through:** `OperationProvenance` positional constructor in `Child` matches the record (12 positional arguments); `RequestErrorKind` resolves via `Cntryl.Portia` as in `LocationAggregateTests`; `NamedRecordFacts` (namespace `Bdgrz.Compliance.Tests`) and `NativeFacts` are reachable; `Assert.Single` on `IEnumerable`; every test has Arrange/Act/Assert and `Should...Given...` names. Not confirmed: runtime values of the history length (4) for `RecordedProcess(…, 2)` rely on accept+apply per revision per `Applied`.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/operationalprocess-test-impl-review-2026-10-29-{request,response}.json`.

| Question | Answer | Disposition |
| --- | --- | --- |
| Tests cover invariant | noul 0.91 | supports |
| Retirement gap disposition (choice) | report_event_change 1.00 (add_to_model 0, remove 0) | applied: no aggregate code; event change reported to coordinator |
| Premise: code contradicts catalog (retirement as content value) | noul 0.93 | gap confirmed |
| Cancel then accept is consistent | noul 0.60 | low confidence, unresolved; settled from code (`CheckAccept` after pending cleared) and the test above |

### Remaining

- Needs shared change: add `OperationalProcessRetired` (EV-D07) schema, discriminator, JSON context entry and catalog promotion; then add the Retire decision and tests.
- Tests unexecuted; build pending. Aggregate remains partial.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
