# CorrectiveAction aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One action (`CorrectiveAction`, stream `corrective-actions/{identity}` under the tenant) owns its materialization, its single completion with evidence, and the finding-closure source-use hold/fence for that action.

- Identity: `AggregateIdentity.Create("CorrectiveAction", tenant, program, finding, action)`. The stream address derives from the tuple only; the selected logical id (command id) is acceptance bookkeeping and cannot fork the stream.
- Invariant: an action is materialized once at revision one, completed once at revision two against the expected revision, and, once a closed-parent settlement fences it, can never be completed or re-held.
- Exclusions: finding closure decision and separation of duties (`FindingAggregate`, closure process), authorization of who may complete (handler), completion note/evidence validation (`FindingRules.ValidateCompletion`), finding view projection.
- Splitting completion from the closure hold would let a completion race a closure; the hold blocks completion on the same stream, so no split.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (own stream) | opened, revision 1 | `CorrectiveActionAddedV2` |
| none | Materialize (foreign stream operation) | validation refusal | none |
| materialized | Materialize | conflict | none |
| none | Complete | not found | none |
| materialized | Complete (expected revision 1) | completed, revision 2 | `CorrectiveActionCompletedV2` |
| materialized | Complete (stale expected revision / no expected revision) | refusal | none |
| completed | Complete | conflict "already completed" | none |
| active closure hold | Complete | transient conflict | none |
| fenced by closed parent | Complete | state conflict | none |
| materialized | Authorize closure use / Settle | per source-use reservations | `...FindingClosureUseAuthorized` / `...Settled` |

## Changes (real gaps)

- Replay guard: `CorrectiveActionCompletedV2` replay checked action and finding id but not tenant and program (the added fact did). It now also compares `TenantId` and `ProgramId` with the opened aggregate (skill rule: stream tenancy alone does not validate payload identity). No test added (would be a malformed-history test, excluded by preference).
- Coverage: the aggregate had only a source-use history test and handler-level tests; added `CorrectiveActionLifecycleTests` for the lifecycle itself.

No other gap found. Not changed: `AuthorizeFindingClosureUse` does not itself require completion; the authorize handler refuses an uncompleted action before pinning, and the grant is revision-pinned.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveStableIdentityFromFindingAndActionGivenSameTuple` |
| Valid materialize / event payload | `ShouldMaterializeAtRevisionOneGivenUnopenedAction` |
| Invalid decision appends nothing | `ShouldRefuseMaterializationGivenOperationOnAnotherStream`, `ShouldRefuseCompletionGivenActionNotMaterialized`, `ShouldRefuseCompletionGivenOperationWithoutExpectedRevision` |
| Changed intent / double decision | `ShouldRefuseSecondMaterializationGivenMaterializedAction`, `ShouldRefuseSecondCompletionGivenCompletedActionReplayed` |
| Completion success | `ShouldCompleteAtRevisionTwoGivenMaterializedActionAndExpectedRevisionOne` |
| Stale revision | `ShouldRefuseCompletionGivenStaleExpectedRevision` |
| Replay of valid history | `ShouldRestoreLifecycleGivenValidMaterializedAndCompletedHistory`, `ShouldRefuseMaterializationGivenReplayedMaterializedAction` |
| Closure hold, permanent fence, bounded history (128 generations) | `CorrectiveActionNativeHistoryTests` |
| Exact retry by command id, competing completion | `CorrectiveActionNativePreparationTests` and the native operation executor (acceptance receipts); framework OCC on append |

Malformed-history tests were not added, by user preference.

## Event contract status

- `CorrectiveActionAddedV2`, `CorrectiveActionCompletedV2`, `CorrectiveActionFindingClosureUseAuthorized`, `CorrectiveActionFindingClosureUseSettled`: business and source-use facts; no add, rename, remove or payload change requested.
- `CorrectiveActionCompletedV2` carries `CompletedByMemberId` and `CompletedBy` (actor reference naming the same member); a possible redundancy, left to the schema owner, not changed.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: completion note/evidence validation stays in `CompleteCorrectiveActionHandler`; retry by command id is the native operation executor's receipt.
- Optional defence in depth: have `AuthorizeFindingClosureUse` require completion at the aggregate (today enforced by the authorize handler).
- Finding projection of the completed action and routes.

## Evidence

- Jev: no request this round; decisions follow the catalog entry (selected split, identity tuple) and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CorrectiveAction"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file and the replay guard are uncompiled; assertion of `RequestErrorKind.NotFound` for the unmaterialized completion relies on `CommandFailure.MissingRecord` mapping.

## Review before build

Independent reviewer pass, 2026-10-29. Nothing built or run; every statement below is from reading source, tests and the Jev response.

### Findings

- Implementation: the completion path enforces the named invariant. A decision is refused with no event when unmaterialized (NotFound), fenced (state conflict), actively held (retryable conflict), stale (version conflict) or already completed (conflict); the replay guard now compares tenant, program, finding and action. No payload field repeats stream identity or metadata beyond what the added fact already carried. Exact retries by command id are acknowledged by the native executor, not this aggregate.
- Test gap found and fixed: the lifecycle table claimed "active closure hold -> Complete -> transient conflict" but no test covered it. Added `ShouldRefuseCompletionGivenActiveFindingClosureUse` (replays Added plus an Authorized fact at revision 1, asserts Conflict, active use still held, revision 1, no pending event).
- Compile review: NotFound mapping confirmed (`CommandFailureCode.MissingRecord` maps to `RequestErrorKind.NotFound`, `StateConflict` to `Conflict`). `Root`, `Raised`, `Replay`, `Ref`, `Now` signatures match their use. `OperationProvenance` constructor arguments in the new test mirror `CorrectiveActionNativeHistoryTests`. Two imports in the lifecycle test (`Operations.Models`, `Remediation.Commands` for command names) are needed or harmless; IDE0005 is not configured.
- Not confirmed: whether `AggregateScenario.Given` accepts a previously attached `Added` instance together with a freshly attached grant in the new test (the same pattern is used for owner-level replays elsewhere, but this exact mix was not executed).

### Jev (model jev-1.14.0, advisory; request/response in implementation-jev/correctiveaction-test-impl-review-2026-10-29-*.json)

| Question | Answer | Probability | Disposition |
|---|---|---|---|
| Refusing completion while a closure use is active is consistent with the rule | consistent_refuse_without_event | 0.99 | Applied; test added |
| Materialization after a closed-parent fence should be refused at the aggregate | refuse_when_fenced | 0.67 (confidence 0.51) | Low confidence, unresolved; code unchanged |
| Authorize should require completion at the aggregate | handler_guard_is_enough | 0.61 vs 0.37 (confidence 0.41) | Low confidence, unresolved; handler guard stays, follow-up only |
| CompletedByMemberId duplicates CompletedBy | keep_both | 0.61 vs 0.34 (confidence 0.41) | Low confidence, unresolved; schema owner decides, no event change |
| Premise: fenced materialization is unreachable | noul 0.27 (premise probably false) | 0.27 | Suggests the unreachable argument is not safe; keep question open |

### Remaining risks

- Open owner decision: should `PrepareMaterialize` refuse once `ClosedByGeneration` is set. Jev leans yes at 0.67 but with low confidence; not applied.
- Open: aggregate-level completion requirement on `AuthorizeFindingClosureUse`.
- Open: `CompletedByMemberId` versus `CompletedBy` redundancy (schema owner).
- The replay guard on the completed fact has no test (malformed-history tests excluded).
- All new and changed code is uncompiled and unexecuted.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
