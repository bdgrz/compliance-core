# AuditorSampleSelection aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Split already in place; keep as its own owner.** `AuditorSampleSelection` (stream `auditor-sample-selections/{identity}` under the tenant realm) is the independently governed exact sample selection, split from `AuditorRequest` (catalog D17-03).

- Identity: tenant, program, request and selection ids, derived through `AggregateIdentity`. The selection id is client-supplied on the admitting command and fixed by the request's admission; no caller key forks the stream, and the same selection id on two requests yields distinct streams (`AuditorSelectionIdentityTests`).
- Invariant: the selection is materialized once, at revision one, on its own stream, from the request's admitted intent. It references population row identifiers and the exact population version content, never the latest population, so a later population version does not move an existing sample. An amendment is a new selection naming the one it supersedes (carried in the frozen intent, not rewritten here).
- Exclusions: admission and the request seal (`AuditorRequest`), verification of the frozen intent, row manifest and population content (`MaterializeAuditorSampleSelectionHandler`), authorization, projections of selections.
- Reducers never load the live request or latest population, so a request that resolved before materialization does not lose the selection.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (own-stream operation) | materialized, revision 1 | `SampleSelectionRecorded` |
| none | Materialize (operation on another stream) | validation refusal, no fact | none |
| materialized | Materialize (exact retry or other operation) | state conflict, no fact | none |
| materialized | exact operation seen again | `Replayed` via acceptance, original fact | none |
| materialized | same logical id, changed intent | `Conflict` via acceptance | none |

## Changes (real gaps)

Source: no change. Identity, single materialization, own-stream operation guard, replay guards (tenant, subject, revision one, owner stream, single fact) and acceptance observation were already in place and match the catalog.

Coverage: the aggregate had only handler/integration coverage. Added `AuditorSampleSelectionTests` (new file, aggregate-only).

Observed, not changed: `Materialize` does not validate the actor member id or `at`; the handler derives both from the verified frozen intent. Not a gap of the invariant.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no key forks the stream | `ShouldDeriveStreamFromTenantProgramRequestAndSelectionGivenOpenSelection`, `ShouldAdmitDistinctFrozenSourcesGivenSameSelectionIdOnDifferentRequests` |
| Materialize success at revision one | `ShouldMaterializeRevisionOneGivenOwnStreamOperation`, `ShouldRecordSelectionFromExactFrozenIncludedRowsGivenPersonalCommand` |
| Refusal with no fact (foreign stream, nulls) | `ShouldAppendNothingGivenOperationNotRootedOnSelectionStream`, `ShouldRefuseNullArgumentsGivenMaterializationAndAppendNothing` |
| No-op / second materialization | `ShouldRefuseSecondMaterializationGivenMaterializedSelectionAndAppendNothing` |
| Exact retry vs changed intent | `ShouldDecideReplayedGivenExactOperationAndConflictGivenChangedIntent`, `AuditorChildMaterializationReplayTests` |
| Unrelated logical id | `ShouldDecideNoneGivenUnrelatedLogicalOperationOnMaterializedSelection` |
| Replay of valid history | `ShouldReplayMaterializedSelectionGivenValidHistory` |
| Competing appends | handler retry on `EventStreamConcurrencyException`; framework OCC (one fact only, later materialization is refused by state) |
| Bounded history | one fact per stream by construction |
| Amended selection lineage | `ShouldAppendAmendedSelectionFromLaterFrozenVersionGivenPersonalCommand` |

Malformed-history tests were not added, by user preference.

## Event contract status

No event add, rename, remove or payload change requested. `SampleSelectionRecorded` carries tenant, subject, revision, actor, the content reference to the frozen intent, occurrence time, provenance and an optional first-response reference (always null here); the selection content itself lives in the immutable intent. Possible redundancy of tenant/subject/revision with the stream address and the always-null `FirstResponse` are left to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: `MaterializeAuditorSampleSelectionHandler` verification stays there.
- Reactor `AuditorSampleSelectionMaterializationReactor`, selection projections and routes.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditorSampleSelection"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file is uncompiled; it assumes `AggregateScenario` of a replayed owner reports no pending events and that `OperationProvenance` supports `with` on `AuthoredIntentSha256`.

## Review before build

Independent review of source and tests, nothing built or run.

### Findings and fixes

- Source: `Materialize` guarded `operation` and `intent` against null but not `operationResult`, so a null result would be stored silently in `SampleSelectionRecorded.OperationResult`. Added `ArgumentNullException.ThrowIfNull(operationResult)` (the handler always passes the unit result). Fixed.
- Test: `ShouldMaterializeRevisionOneGivenOwnStreamOperation` did not assert the intent reference (`Change`) or the `OperationResult`, so a swapped or dropped reference would pass. Now builds distinct refs and asserts both. Fixed.
- Test: null-argument test now also covers a null `operationResult`. Fixed.
- Confirmed by reading: usings and APIs (`NativeFacts.Root/Ref/Raised/Replay`, `AggregateScenario.PendingEvents.Count`, `AcceptedOperationBinding.Of`, `NativeAcceptanceDecision`) exist as used; `Raised` is called once on the seed owner; no `DomainEventSeed.Attach` on pending events; names and Arrange/Act/Assert phases follow CNTRYL0001/0002; `OperationProvenance` is a record so `with` compiles.
- Implementation checked against the invariant: single materialization, revision one, own-stream guard (command and replay), acceptance observed before guards; exact retries are acknowledged by the handler through acceptance with no new event.

### Jev (model jev-1.14.0, advisory), request and response in `implementation-jev/auditorsampleselection-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability |
|---|---|---|
| Second-materialization test proves no duplicate fact | true | 0.88 |
| Acceptance test proves exact retry vs changed intent | true | 0.93 |
| Premise: TenantId/SubjectId/Revision repeat stream identity or a constant | true | 0.92 |
| Payload redundancy disposition | keep_as_is 0.82, remove_redundant 0.16, needs_source_resolution 0.02 | confidence 0.73 |

Discrepancy disposition: redundant payload fields (TenantId, SubjectId, Revision, always-null FirstResponse) are a schema-owner matter; Jev leads decisively for keep_as_is, so no code change and no event change is requested. The event is in the catalog and is not renamed.

### Remaining risks

- Uncompiled and unexecuted. Unconfirmed: that `AggregateScenario` reports no pending events for a replayed owner (the sibling `AuditorFeedbackThreadTests` relies on the same behavior) and that the JSON round trip preserves `ActorReference` equality for the `Replayed` decision.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
