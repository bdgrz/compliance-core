# ExaminationSignOff aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain, per catalog).** One sign-off (`examination-signoffs/{SignOffId}` under the tenant) is one immutable management decision capsule.

- Identity: caller-supplied immutable `SignOffId` is the creation identity and the stream key; no request id or content field forks the stream.
- Invariant: one final personal management decision binds exact package, assertion, letter and outcome identities and acknowledges exactly every unresolved material item.
- Exclusions: package/assertion/letter/outcome lifecycles (independent records), D17-07 sign-off sequencing across those records (needs reading other aggregates: handler/reactor concern), authorization policy, projections, routes.
- Splitting known exceptions or acknowledgements from the decision would permit signing a different set, so they stay together.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | SignOff (valid, named CEO/CISO signer, role matches) | revision 1 | `ExaminationSignedOff` |
| none | SignOff (invalid content, acknowledgement mismatch, bad attribution) | invalid content | none |
| none | SignOff (actor not a named executive approver, or role mismatch) | actor prohibited | none |
| signed | SignOff (same content, actor, role, time) | no-op acknowledged | none |
| signed | SignOff (changed content, actor, display, or time) | state conflict | none |

## Changes (real gaps)

Source: `SameInitial` compared the stored approver role (taken from the approver list, e.g. `CEO`) to the caller's role case-sensitively, while admission accepts roles case-insensitively. An exact retry with `ceo` after a first accepted call with `ceo` was refused as a conflict. Now compared with `OrdinalIgnoreCase`.

Tests added: role-case retry, changed-intent refusal, extra acknowledgement, role mismatch, bad hash and missing attribution append nothing, replayed retry and changed intent.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Named invariant, exact acknowledgement | `ShouldBindManagementApprovalToExactInputsAndAcknowledgeEveryOpenItemGivenOutcome`, `ShouldAppendNoFactGivenAcknowledgementThatDiffersFromUnresolvedItems` |
| Signer authority | `ShouldRequireAuthorizedManagementSignerGivenCompleteExternalOutcome`, `ShouldAppendNoFactGivenSignerRoleThatDiffersFromNamedApprover` |
| Invalid decision appends nothing | `ShouldAppendNoFactGivenInvalidHashOrMissingAttribution` |
| Lifecycle content (commitments) | `ShouldRetainAttributedFinalCommitmentsAndDueDatesGivenTypeIISignOff` |
| No-op / exact retry | `ShouldAcknowledgeExactRetryWithoutNewFactGivenSignOffAlreadyRecordedWithDifferentRoleCase` |
| Changed-intent refusal | `ShouldRefuseChangedIntentGivenSignOffAlreadyRecorded` |
| Replay | `ShouldReplayImmutableManagementDecisionGivenSignOffEvent`, `ShouldRetryExactlyAndRefuseChangedIntentGivenReplayedSignOff` |
| Stale revision / competing appends | Single-fact aggregate: a second decision is a conflict or no-op by the immutability tests above; physical append position is the framework's. |
| Bounded history | One fact; item counts and event payload size are bounded in source. |

## Event contract status

`ExaminationSignedOff`: no add, rename, remove or payload change requested. The catalog marks it "requires schema reconciliation" at the schema owner; the payload carries the complete view (business facts) plus actor and time.

## Later-stage follow-ups (not aggregate gaps)

- D17-07 ordering (package validated, assertion signed, letter signed, then outcome) must be enforced by the handler against the source aggregates.
- Handler reuses retained `SignedOffAt` for retries; authorization precedes replay.
- Projectors and route for the management review.

## Evidence

- Jev: no request; decisions follow the catalog entry.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ExaminationSignOff"`.
- RED: none captured; not claimed.

## Review before build

Reviewer: independent skeptic pass. Nothing built or run; tests are written, not executed.

### Findings and fixes

- Source: the role-case `SameInitial` fix is correct (admission and retry now both case-insensitive; stored role is the trimmed approver-list role). No further implementation gap found: retry precedes validation, stored facts are normalized, acknowledgement is compared as an id set, a second approver or changed intent is a state conflict.
- Test gap fixed: added `ShouldAppendNoFactGivenAcknowledgementOfDifferentItemsWithSameCount` (set equality, not count), `ShouldAcknowledgeExactRetryWithoutNewFactGivenAcknowledgementsInDifferentOrder` (normalized ordering retry) and `ShouldRefuseSecondApproverGivenSignOffAlreadyRecordedByOtherApprover` (competing approver).
- Compile read: new tests use collection expressions into `IReadOnlyList<Uuid>`/`IReadOnlyList<ManagementAssertionSigner>` (same form as existing tests), `Assert.Single`, `CommandFailureCode`, ordered phases, valid `Should...Given...` names. Rejected-decision path is not separately tested (decision string is validated and bound by digest).

### Jev (model jev-1.14.0; request/response in `implementation-jev/examinationsignoff-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability | Disposition |
|---|---|---|---|
| Role-case retry outcome | success, no new fact | 1.00 | Applied (matches code and test). |
| D17-07 ordering location | later handler stage | 0.79 (conf 0.72) | Decisive lead; aggregate keeps only its identity-binding invariant; ordering stays a handler follow-up. |
| Payload duplication (TenantId, SubjectId, ActorMemberId, OccurredAt, State identity) | remove from payload in later wire-versioned change | 0.93 | Not applied here: event/schema change, listed under remaining for the schema owner. |
| Premise: payload repeats identity/metadata | true | 0.82 | Holds. |
| Tests assert rule and invariant (original four-test summary) | true | 0.27 | Low: unresolved. Three tests were added after this (set equality, order, competing approver) in response; not re-asked. |

### Remaining risks

- Event payload schema reconciliation (remove duplicated identity/actor/time fields) is a shared-file change: not applied.
- D17-07 ordering enforcement is unimplemented until the handler stage.
- Nothing compiled or executed.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
