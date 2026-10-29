# AdvisorHandoff aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain) as the catalog's selected split target.** One handoff (`AdvisorHandoff`, stream `advisor-handoffs/{handoffId}` under the tenant) decides atomically its small selection and readiness state, its closure or revocation, and which feedback may be admitted. Feedback content and its correction/response lifecycle live in the separate `AdvisorFeedbackThread`.

- Identity: tenant + caller-supplied immutable `HandoffId`. The stream address derives from those only; mutations are bound by `CommandId` to this identity and the expected revision (`OperationProvenance`).
- Invariant: a handoff selects a bounded set of exact record versions; only a draft can be marked `ready_for_review`; feedback is admitted only against a ready, unrevoked, unclosed, unexpired handoff at the current revision and generation; closing or revoking ends admission with history retained. Revocation, closure and admission share this stream, so they serialize.
- Exclusions: selection content and its bound of 100 (verified content and handler), source-version resolution and exact-selection proof, feedback bodies and correction/response/resolution (`AdvisorFeedbackThread`), guest/advisor authorization, listing and previews.
- Splitting admission from revocation would let an admission race a revocation; no further split.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Create (valid) | draft, revision 1, generation 1 | `AdvisorHandoffCreated` |
| none | Create (self or empty comparison, foreign operation logical id, empty program) | validation refusal | none |
| none | Revise, Ready, End, Admit | not found | none |
| created | Create again | conflict | none |
| open | Revise, Ready, End, Admit with a stale revision | conflict | none |
| draft or ready | Revise | draft, generation + 1 | `AdvisorHandoffSelectionRevised` |
| draft | Ready (unexpired) | ready_for_review | `AdvisorHandoffReadiedForReview` |
| draft | Ready (expired) | validation refusal | none |
| ready | Ready again | conflict | none |
| draft or ready | Close / Revoke | closed / revoked | `AdvisorHandoffClosed` / `AdvisorHandoffRevoked` |
| ready | Admit (current revision, unexpired) | revision + 1, admission count + 1 | `AdvisorFeedbackAdmitted` |
| draft or expired | Admit | conflict | none |
| closed / revoked | any change or admit | conflict | none |

Exact retry and changed intent are decided by the operation (`NativeAcceptance`, `AdvisorReplayCut`) and the handler receipts; see the handler and native-history suites.

## Changes (real gaps)

- `Create` accepted a comparison baseline equal to the handoff itself or an empty id, and an empty program id. Replay (`Apply(AdvisorHandoffCreated)`) rejects exactly those, so the command could append a fact that the stream could never be hydrated from again. `Create` now refuses them as validation before appending. No other gap was found in identity, lifecycle, revision or admission checks.

## Test-to-criterion table

New file `AdvisorHandoffAggregateTests.cs`; existing handler and native suites stay as they are.

| Criterion | Test |
|---|---|
| Valid creation, payload | `ShouldAppendCreationFactGivenValidTermsAndComparisonBaseline` |
| Invalid decision appends nothing | `ShouldRefuseCreationAndAppendNothingGivenComparisonToItself`, `...GivenEmptyComparisonBaseline`, `...GivenOperationOfAnotherLogicalId` |
| Refusal with no fact | `ShouldReportNotFoundGivenHandoffNeverCreated` |
| Second creation refused | `ShouldRefuseSecondCreationGivenHandoffAlreadyCreated` |
| Stale revision | `ShouldRefuseGivenStaleExpectedRevisionOnDraftHandoff`, `ShouldAdmitRepeatedlyGivenReadyHandoffAndCurrentRevision` |
| Readiness and expiry | `ShouldRefuseReadyGivenExpiryPassed`, `ShouldRefuseSecondReadinessGivenHandoffAlreadyReady` |
| Generation advance, historical feedback boundary | `ShouldAdvanceGenerationAndReturnToDraftGivenSelectionRevisedAfterReadiness` |
| Admission authority | `ShouldRefuseAdmissionGivenDraftOrExpiredHandoff`, `ShouldAdmitRepeatedlyGivenReadyHandoffAndCurrentRevision` |
| Terminal states | `ShouldRefuseEveryChangeGivenEndedHandoff` (close, revoke) |
| Replay of valid history | `ShouldRetainCreationTermsGivenReplayAfterLaterFacts` and the Given-replayed tests above |
| Competing revocation vs admission, exact retry after later facts, changed intent, bounded selection | existing `AdvisorFeedbackAdmissionTests`, `AdvisorHandoffCommandHandlerTests`, `AdvisorHandoffTests`, `AdvisorNativeHistoryTests` |

Direct aggregate commands after the first need a finalized native cut that only the executor confirms, so the lifecycle checks are exercised through the public `Prepare*` decisions on replayed history. Malformed-history tests were not added, by user preference.

## Event contract status

- `AdvisorHandoffCreated`, `...SelectionRevised`, `...ReadiedForReview`, `...Closed`, `...Revoked`, `AdvisorFeedbackAdmitted` carry business facts plus operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: validate comparison baseline and program before calling `Create` (the aggregate now also refuses).
- `AdvisorFeedbackThread` materialization reactor and receipt binding.
- HTTP routes and authorization precedence before replay.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and replay invariants in the source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AdvisorHandoff"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: blind-written test compile errors; existing handler tests that create with an empty program id or self comparison (none found by reading) would now get a validation refusal.

## Review before build

Independent review, 2026-10-29. Nothing built or run.

### Findings
- Implementation: Create refusal ordering is sound (own-operation check, then program/baseline validation, then existing-stream conflict, then the native cut). Every refusal precedes `RaiseEvent`, and the refused cases match exactly the checks `Apply(AdvisorHandoffCreated)` throws on, so no unreplayable fact can be appended. Lifecycle, revision and generation checks match the catalog decision. No payload field repeats stream identity or metadata beyond the existing event contracts. No fix needed.
- Exact retries are resolved by the native acceptance layer, not the aggregate; a second Create is Conflict with no event, which is consistent.
- Tests: each assertion checks an outcome (error kind, pending facts, status, revision, generation, admission count). Fixtures were checked against `NativeFacts.Root`, `RootOperation.Provenance`, `DomainEventSeed.Attach` on fresh events, the event record constructors, and `AdvisorReplayCut.Require` (fresh owner, version 0, passes). Naming, phase comments and `.Count` pitfalls: none found. Replay revisions (ready=2, admit=3, close=4) were recomputed by hand and match the assertions.
- Gap left open: the aggregate does not enforce the 100-item bound or the Compliance Lead role; the selection is opaque content and authorization is excluded by the aggregate's stated boundary. Recorded as handler/validator follow-ups. Success paths of `MarkReady`, `Close`, `Revoke` and `AdmitFeedback` cannot be driven from an owner test without an executor-finalized cut and are covered by the existing handler and native-history suites.

### Fixes
- None required beyond the implementer's Create hardening.

### Jev (model jev-1.14.0)
Files: `implementation-jev/advisorhandoff-test-impl-review-2026-10-29-request.json` and `-response.json`.
- Create with self/empty baseline or empty program: refuse as validation, append nothing, 0.99 (decisive; matches code).
- Refusing admission after a selection revision returned the handoff to draft: consistent with the invariant, Noul 0.96.
- Validation for expired ready versus Conflict for expired admission: acceptable difference, 0.99.
- Retry resolution outside the aggregate; duplicate Create is Conflict: Noul 0.91.
- Tests assert the invariant: Noul 0.65, moderate. Residual reason: the Compliance Lead role and the selection bound are outside the aggregate's tests, as above. Treated as unresolved, not approval.

### Discrepancies
- No event or command mismatch with the catalog was found; no event, schema or catalog change is needed.

### Remaining risks
- Compile and runtime unverified. Unconfirmed items: `Assert.Equal(int, long)` generic inference on Revision/Generation, and whether strict analyzers flag the `DomainEvent fact = step switch` explicit type.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
