# FindingAggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One finding (stream `findings/{StreamId(programId, findingId)}` under the tenant) decides atomically its raised terms, triage, acceptance links, the admission of corrective actions per generation, and the closure decision.

- Identity: tenant + program + caller-supplied immutable `FindingId`; stream id is `Uuid.CreateVersion5(programId, "finding:{findingId}")`. No request key forks the stream. Raise must be the finding's own logical operation.
- Invariant: a finding closes only after at least one admitted action and an accepted closure that seals admission for the generation; a closed finding is immutable until reopened; each reopen advances the generation.
- Exclusions (other owners/processes): action content and completion (`CorrectiveAction`), completion proofs, evidence/rationale, separation of duties and waivers (closure process, authorizers), readiness status (projection), authorization.
- Split already applied: actions are admitted here and materialized as their own owner via `CorrectiveActionMaterializationReactor`; history rosters are projections. Bounded state: reducers keep only the latest fact per concern plus counters (see `FindingNativeHistoryTests`).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Raise (own operation) | raised, optional initial action | `FindingRaisedV2` (+ `CorrectiveActionAdmitted`) |
| none | Raise (foreign operation or foreign initial-action owner) | validation refusal | none |
| none | any write | not found | none |
| raised | Raise again | conflict | none |
| open | Revise / LinkAcceptance / AdmitAction | applied at expected revision | `FindingRevisedV2` / `FindingAcceptanceLinkedV2` / `CorrectiveActionAdmitted` |
| open | any write at stale revision | conflict | none |
| open | AdmitAction with an already admitted action id | conflict | none |
| open, no admitted action | AcceptClosure | conflict (acceptance is not remediation) | none |
| open | AcceptClosure (valid, root operation) | closure pending, admission sealed | `FindingClosureAccepted` |
| pending | any write or second closure | retryable conflict | none |
| pending | CompleteClosure (exact pending root and intent) | closed | `FindingClosedV2` |
| pending | CompleteClosure (other intent) / CancelClosure (other root) | conflict | none |
| pending | CancelClosure (exact) | open again | `FindingClosureCanceled` |
| closed | write | conflict ("reopen it") | none |
| closed | Reopen at expected revision | open, generation + 1 | `FindingReopenedV2` |
| open | Reopen | conflict | none |

## Changes (real gaps)

- `Raise` with an `InitialActionAdmission` whose operation belonged to another stream appended the raised fact and then threw from the reducer on the second fact, leaving a half-applied aggregate and an exception instead of a refusal. It now returns a validation refusal before any fact.
- `AcceptClosure` accepted an operation whose `RootOperationId` differs from its `LogicalId` and then threw from the reducer. It now returns a validation refusal before any fact (a closure is a root operation).
- No other behavior change. Command-level coverage already existed in handler/fixture tests (`FindingTests`, `FindingClosureTests`, `FindingIdentifiedOperationTests`, `FindingHistoricalClosureTests`, `FindingNativeHistoryTests`); the task note "tests none found" was inaccurate. There was no owner-level test file, which is added.

## Test-to-criterion table (new `FindingAggregateTests`)

| Criterion | Test |
|---|---|
| Stable identity, raise at revision one | `ShouldRaiseAtRevisionOneGivenOwnFindingOperation` |
| Atomic initial action | `ShouldAdmitInitialActionAtomicallyGivenRoutedRaise` |
| Invalid decision appends nothing | `ShouldRefuseRaiseGivenOperationOfAnotherFinding`, `ShouldRefuseRaiseGivenInitialActionOfAnotherStream`, `ShouldRefuseClosureGivenOperationWhoseRootIsAnotherOperation`, `ShouldRefuseOrdinaryWriteGivenOperationWithoutExpectedRevision` |
| Refusal with no fact (missing record) | `ShouldRefuseRevisionGivenFindingNeverRaised` |
| No-op / duplicate raise | `ShouldRefuseRaiseGivenFindingAlreadyRaised` |
| Stale revision / competing decisions | `ShouldRefuseSecondWriteGivenSameExpectedRevision`, `ShouldRefuseReopenGivenOpenFindingOrStaleRevision` |
| Triage state, source text preserved | `ShouldReviseCurrentTermsGivenOpenFinding` |
| Closure needs remediation, acceptance is not remediation | `ShouldLinkAcceptanceWithoutAdmittingRemediationGivenOpenFinding`, `ShouldRefuseClosureGivenNoAdmittedAction` |
| Changed intent / action identity reuse | `ShouldRefuseActionIdentityReuseGivenActionAlreadyAdmitted`, `ShouldRefuseCompletionGivenChangedClosureIntent` |
| Sealed admission while pending | `ShouldSealAdmissionAndWritesGivenPendingClosure` |
| Closed is immutable | `ShouldCloseAndFreezeFindingGivenExactPendingClosure` |
| Fenced completion/cancel | `ShouldRefuseCompletionAndCancelGivenNoPendingClosure` |
| Reopen, generation advance, new-generation admission | `ShouldReopenAtNextGenerationAndAdmitActionGivenClosedFinding` |
| Replay of valid history; retry of committed facts after later facts; historical closure resolution | `ShouldRehydrateAndResolveCommittedFactsGivenRetryAfterLaterFacts` |
| Bounded history | existing `FindingNativeHistoryTests` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `FindingRaisedV2`, `FindingRevisedV2`, `FindingAcceptanceLinkedV2`, `CorrectiveActionAdmitted`, `FindingClosureAccepted`, `FindingClosedV2`, `FindingClosureCanceled`, `FindingReopenedV2` carry business facts and operation provenance only. No add, rename, remove or payload change requested.
- The catalog `baselineEvents` list omits the closure-accepted, canceled and action-admitted facts; the entry's `events` list (selected final catalog) includes them. Catalog baseline lists are the shared owner's to reconcile.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: `RaiseFindingHandler`, `RaiseOccurrenceFindingHandler`, `CloseFindingHandler`, `ReopenFindingHandler` and friends own authorization precedence before replay, CommandId receipts and original-result binding.
- Reactors/process: `FindingClosureReactor` and the closure process own action completion proofs, evidence/rationale and separation-of-duties checks (`CompleteFindingClosure` / `AbortFindingClosure`).
- Catalog `baselineSourcePath` points at the old `Features/Remediation/FindingAggregate.cs`; the current path is `Features/Remediation/Aggregates/FindingAggregate.cs` (tracker owner to update).

## Evidence

- Jev: no request this round; decisions follow the catalog invariant and the existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~FindingAggregateTests"`, then the Finding* suites and the unit suite.
- RED: none captured; not claimed.
- Residual risk: the two refusals now return `Validation` where the old code threw from the reducer; handler tests that relied on the exception (none found) are unverified. Test provenance helper mirrors `FindingNativeHistoryTests` and has not been compiled.

## Review before build

Independent review of source and tests, read as the compiler and analyzers would. Nothing built or run.

Findings:
- No correctness gap found. Both new refusals precede any `RaiseEvent`, so no half-applied aggregate remains; every reducer guard is reachable only through a command that pre-checks it (raise, closure root, admission once, exact pending closure, reopen from closed).
- Compile check: constructor arities of `FindingRaiseTerms` (11), `FindingRevision` (8), `FindingSource` (4), `OperationProvenance` helper and `with { FirstOwner }`, `InitialActionAdmission`, `ChildAdmissionIntent.Schema`, `ClosureIntent.Schema`, `NativeFacts.Ref/Now/Raised/Replay`, `AggregateScenario.PendingEvents.Count` (list property) and `owner.Version` as `ulong` all match their definitions. Usings cover Proofs.Models, AccessControl.Models, NativeOperations.Models, Remediation.Commands/Events/Models/Services. Revision arithmetic and pending counts in each test were traced by hand and agree.
- Replay test uses `NativeFacts.Raised` (clone and attach) and captures state before the retry command. No events from `PendingEvents` are passed to `Attach`.
- Residual, not changed: `Raise` does not reject an initial action whose logical id equals the finding id; not shown to be reachable from handlers. Action and ordinary-write operations are not required to be root operations in the reducer; they are only checked for owner and expected revision.

Jev (model jev-1.14.0), request and response in `implementation-jev/findingaggregate-test-impl-review-2026-10-29-*.json`:
- Closure requires an admitted action and pending closure seals writes enforces the invariant: noul 0.84 (true).
- Foreign initial action on raise: validation refusal with no fact, probability 1.0.
- Reopen advancing generation consistent with invariant: noul 0.92.
- Tests assert behavior and can fail: noul 0.94.
- Re-admitting an admitted action id: conflict with no new fact, probability 1.0 (handlers resolve exact retries to the original result via `HasAdmitted`/`Acceptance` before calling the owner).
- Discrepancies: none requiring a catalog decision. The baselineEvents omission of three events is a stale baseline list; the final `events` list has them (shared owner to reconcile).

Remaining: build and run `FindingAggregateTests`, then Finding* suites and the unit suite; tracker row 31 and catalog baselineSourcePath updates belong to the shared owner.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
