# AuditorFeedbackThread aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep as the catalog's selected split target (`selected_split`).** One thread (stream `auditor-feedback-threads/{identity}` under the tenant realm) is materialized from the request's `AuditorCommentAdmitted` fact at revision one and is otherwise immutable; no reply policy exists in the catalog or the code. The thread never loads the live request, so a later resolution cannot discard an admitted comment.

- Identity: tenant + program + request + thread (comment) id via `AggregateIdentity.Create`; the thread id is the admitted comment id. The admitting reactor (`AuditorCommentMaterializationReactor`) establishes it. The selected logical id only selects acceptance bookkeeping and never forks the stream.
- Invariant: a comment thread is materialized once, at revision one, on its own stream, from the admitted comment; thereafter it is immutable. (Catalog statement: "Independent comments and responses with stable command identity"; must-preserve "Admission cannot race resolution".)
- Exclusions: admission and the seal against resolution (`AuditorRequest`), frozen admission intent verification (`MaterializeAuditorCommentHandler`), comment body and compartment content (immutable content store), authorization, listing projections.
- Split/reactor decision: no further split. Coordination with the request stays in the materialization reactor.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (own stream) | recorded, revision 1 | `AuditorRequestCommentRecorded` |
| none | Materialize (operation on another stream) | validation refusal | none |
| recorded | Materialize again (any operation) | conflict | none |
| recorded | exact retry of the recording operation | `Acceptance` decides Replayed, no append | none |
| recorded | same logical id, changed binding | `Acceptance` decides Conflict | none |

There is no other command; revision is 0 before and 1 after materialization.

## Changes

Source: no change. The adversarial pass found identity derivation, own-stream guard, once-only materialization and the replay guard (tenant, subject, revision one, own stream, once) already in place. Parity with `AuditorResponse` and `AuditorSampleSelection` materialization was checked. Author, intent and frozen header verification are the handler's job and are not duplicated here.

Coverage: there were no aggregate-level tests (the thread was only exercised through `AuditorRequestCommandHandlerTests` and the receiver replay tests). Added `AuditorFeedbackThreadTests.cs`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks the stream | `ShouldDeriveStableIdentityGivenSameTenantProgramRequestAndThread` |
| Valid materialization, payload, revision one | `ShouldAppendRecordedFactAtRevisionOneGivenAdmittedComment` |
| Invalid decision appends nothing | `ShouldRefuseMaterializationAndAppendNothingGivenOperationOnAnotherStream` |
| Second materialization refused | `ShouldRefuseSecondMaterializationGivenThreadAlreadyRecorded` |
| Fresh thread may materialize | `ShouldAcceptMaterializationGivenFreshThread` |
| Replay of valid history | `ShouldHydrateMaterializedThreadGivenReplayOfRecordedFact` |
| Replayed thread refuses again, no fact | `ShouldRefuseMaterializationAndAppendNothingGivenReplayedThread` |
| Exact retry | `ShouldDecideReplayGivenExactBindingOfRecordedOperation` |
| Changed intent | `ShouldDecideConflictGivenChangedIntentOfRecordedOperation` |
| No accepted operation | `ShouldDecideNoneGivenThreadWithoutRecordedOperation` |
| Handler-level materialization, replay after request resolution | existing `AuditorRequestCommandHandlerTests`, `AuditorChildMaterializationReplayTests` |
| Stale revision, competing decisions, bounded history | not applicable: the thread has one fact and no expected-revision command; framework OCC covers concurrent creation and the handler retries on `EventStreamConcurrencyException` |

Malformed-history tests were not added, by user preference.

## Event contract status

`AuditorRequestCommentRecorded` carries `TenantId`, `SubjectId` and `Revision` alongside the stream address (a possible redundancy left to the schema owner), the frozen intent reference, actor, time and operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: `MaterializeAuditorCommentHandler` already verifies the frozen admission; no change needed.
- Reactors: `AuditorCommentMaterializationReactor` binding (exists; later-stage verification).
- Projectors: comment thread listing.
- Product: replies to a thread are not in the catalog; if wanted they need a new decision, event and tests.

## Evidence

- Jev: not consulted; no open design decision (the catalog and source agree).
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditorFeedbackThread"`, then the unit suite.
- RED: none captured; no source change was needed.
- Residual risk: blind-written tests may have compile errors (for example property names on `EventStreamAddress`).

## Review before build

Reviewer: independent pass, nothing built or run.

Findings and fixes:

- `ShouldDecideConflictGivenChangedIntentOfRecordedOperation` changed the command kind, not the intent, so it did not match its name. Fixed: it now alters only `AuthoredIntentSha256` of the recorded provenance (same pattern as `AuditorSampleSelectionTests`).
- `Assert.Same(fact, owner.Recorded)` depended on the scenario returning the very instance the aggregate applied. Relaxed to `Assert.Equal` (record equality), which still fails on a wrong fact.
- Verified by reading: handler decides `Acceptance.Decide` before `Materialize` (exact retry acknowledged without an event); `EventStreamAddress.Realm`/`Area` exist and are used by other tests; `NativeAcceptance.Observe`/`Decide` and `AcceptedOperationBinding.Of` signatures match the tests; usings cover `Uuid`, `Result`, `DomainEvent`, `RequestErrorKind` (Cntryl.Portia), `AggregateScenario`, `NativeFacts`, `ImmutableContentRef`, `OperationProvenance`.
- Source: no defect found. Apply guards tenant, subject, revision one, own stream and once-only.

Jev (model jev-1.14.0; request and response in `implementation-jev/auditorfeedbackthread-test-impl-review-2026-10-29-*.json`):

| Question | Answer | Probability |
|---|---|---|
| Second Materialize on recorded thread | conflict_no_event | 1.00 |
| Tests verify replay and changed-intent conflict | true | 0.92 |
| Payload redundancy handling | leave_to_schema_owner | 0.59 (remove_fields 0.33), low confidence, unresolved |
| Premise: TenantId/SubjectId/Revision repeat stream identity | noul | 0.54, near-even, unresolved |

Discrepancy disposition: the payload redundancy (`TenantId`, `SubjectId`, `Revision`) stays unresolved and is reported to the schema owner; no event change applied. Jev is advisory only.

Remaining risks: tests not compiled or run; `Assert.Equal(fact, owner.Recorded)` and `with` on `OperationProvenance` assume record semantics as seen in sibling tests.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
