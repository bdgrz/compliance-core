# AuditorResponse aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain the selected split).** One response (`AuditorResponse`, stream `auditor-responses/{identity}` under the tenant realm) owns its draft versions, independent disclosure review, delivery and external disposition. The parent `AuditorRequest` admits the response and later resolves over the exact set; this owner is materialized from that admission at revision one.

- Identity: `AggregateIdentity.Create("AuditorResponse", tenant, program, request, response)`. No caller key forks the stream; `CommandId` binds the native operation and expected revision (acceptance bookkeeping through `NativeAcceptance`).
- Invariant: a version moves draft -> submitted -> approved/rejected -> delivered -> disposed exactly once each; review is independent of the author; delivery and disposition name the exact version and are immutable; a request resolution hold blocks changes until settled; a verified `ParentClosed` settlement fences the response permanently.
- Exclusions: content verification, authorization, evidence reading, the resolution decision and process (`AuditorRequest`, reactor), projections of response history.
- Splitting the review/delivery chain apart would let a delivery race a review of the same version; the chain is decided on this one stream, so no further split. History is bounded per business version by the draft/revise loop only, and the retained state is scalar (existing 128-revision bound test).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (own stream) | draft, revision 1, version 1 | `AuditResponseDraftCreated` |
| materialized | Materialize | conflict | none |
| draft / rejected | Revise (expected revision) | draft, version + 1 | `AuditResponseDraftRevised` |
| draft | Submit (exact version) | submitted | `AuditorResponseSubmitted` |
| submitted | Review (independent, exact version) | approved / rejected | `AuditorResponseDisclosureReviewed` |
| submitted / approved | Deliver (approved when internal review required) | delivered | `AuditorResponseDelivered` |
| delivered | Record disposition (exact delivered version) | disposed | `AuditorResponseDispositionRecorded` |
| delivered / disposed | Revise, Deliver, Disposition again | state conflict | none |
| any content state | active resolution hold | transient conflict | none |
| any | stale expected revision / version | version conflict | none |
| fenced (ParentClosed settled) | any write | state conflict | none |
| held / settled | Authorize / Settle resolution use | no revision change | use grant / settlement facts |

## Changes (real gaps)

Source: no change. The adversarial pass found identity, own-stream operation roots, author-independent review, exact-version guards, delivery and disposition immutability, the hold and permanent fence, and replay guards (tenant, subject, next revision, owner stream) already in place.

Coverage: the post-delivery and post-disposition refusals, other-version disposition, internal-review delivery rule, rejected-then-revise path and retry-after-later-fact were not asserted at the aggregate. Added `AuditorResponseLifecycleTests` (new, aggregate-only).

Observed, not changed: `Review` and `RecordDisposition` do not validate the outcome token themselves; the handlers normalize it through `AuditorRequestRules` before the aggregate runs, and the reducer rejects unknown review outcomes on raise. Left as a handler-stage concern.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, materialization, draft success | `AuditorResponseCommandHandlerTests` |
| Independent reviewer, delivery, disposition, resolution | `ShouldReviewDeliverAcceptAndResolveResponseGivenIndependentReviewer` |
| Immutable delivery and disposition, no fact | `ShouldRefuseRevisionDeliveryAndSecondDispositionGivenDispositionRecorded` (new) |
| Exact-version disposition, stale revision | `ShouldRefuseDispositionOfOtherVersionGivenDeliveredResponse` (new) |
| Internal review required before delivery | `ShouldRequireApprovalGivenSubmittedResponseWhenInternalReviewRequired` (new) |
| Rejected -> revise lifecycle | `ShouldReviseIntoNextDraftVersionGivenRejectedReview` (new) |
| Exact retry after later fact, changed intent refused | `ShouldReturnOriginalResultAndRefuseChangedIntentGivenSubmitRetriedAfterLaterFact` (new) |
| Competing append, replay of equal winner | `ShouldReplayEqualSubmittedWinnerGivenLosingUnitFreezeFailsAfterConcurrentAdmission` |
| Permanent fence, original grant kept, bounded replay | `ShouldKeepOriginalFenceAndSelectedGrantGivenManyResolvedParentGenerations` |
| Bounded history | `ShouldKeepBoundedCommandAuthorityGivenManyValidDraftVersions` |

Malformed-history tests were not added, by user preference.

## Event contract status

No event add, rename, remove or payload change requested. Content facts carry actor, content refs, version and outcome; tenant, subject and revision accompany the stream address, a possible redundancy left to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: outcome token normalization stays in the review and disposition handlers.
- Resolution process, response history projection and routes.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditorResponse"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file is uncompiled; error kinds assume `CommandFailure.StateConflict` surfaces as `Conflict`, and the changed-intent test asserts only refusal.

## Review before build

Independent review of the implementer's tests and the unchanged source. Nothing compiled or run.

### Findings and fixes

1. **Real test bug (fixed).** `ShouldRefuseDispositionOfOtherVersionGivenDeliveredResponse` delivered a submitted but unreviewed response through `DeliverAsync`. `DeliverAuditResponseHandler` defaults `requiresReview` to true (`AuditorCommands.RequiresReviewAsync`), and `AuditorRequestTests` already shows an early delivery being refused with Conflict. The Arrange `Assert.True` would have failed before the Act. Added the independent `ReviewAsync` step before delivery.
2. **Phase hygiene (fixed).** In `ShouldReviseIntoNextDraftVersionGivenRejectedReview` the `PrepareDeliver` on the rejected response ran in Arrange; moved to Act.
3. Source re-read: identity, own-stream operation roots, author-independent review, exact-version guards, delivery and disposition immutability, hold, permanent fence and reducer guards confirmed. Native acceptance is consulted before `Prepare` in `NativeOperationExecutor`, so a retry of a committed submission is replayed without a new fact; this supports the retry test.
4. `StateConflict` maps to `RequestErrorKind.Conflict` (`CommandFailureRequestAdapter`), so the Conflict assertions are consistent. `VersionConflict` cases are asserted only as `IsSuccess == false`.
5. Compile read: usings cover `Uuid`/`RequestErrorKind` (Cntryl.Portia), `ProcessState`, commands and handlers; constructor argument orders for `SubmitAuditResponse` and `ReviseAuditResponseDraft` match their records; no `.Count()`, no inline arrays beyond `[]` collection expressions already used by sibling tests.

### Jev (jev-1.14.0, advisory), request/response in `implementation-jev/auditorresponse-test-impl-review-2026-10-29-*.json`

| Discrepancy / question | Answer | Probability |
|---|---|---|
| Review with an outcome outside approved/rejected: refuse in command, or reducer throws | throw_in_reducer | 0.92 (refuse_in_command 0.03) |
| RecordDisposition with an outcome outside accepted/rejected/changes_requested: refuse in command, or record any | record_any | 0.94 (refuse_in_command 0.04) |
| Premise: aggregate must itself refuse unsupported disposition tokens | noul | 0.45 (not supported) |
| Premise: retry test asserts original ack with no new fact, changed intent refused | noul | 0.91 |

Disposition: decisive, so no aggregate change; token validation stays in the handlers (`AuditorRequestRules.Review/Disposition`), recorded as a handler-stage follow-up. Reviewer note: this leaves a direct aggregate caller able to record a free-text disposition outcome; revisit if a non-handler writer is ever added. No event, schema or catalog change requested.

### Remaining risks

- Changed-intent assertion checks refusal only, not the error kind (the `AcceptedAppendResult.Conflict` mapping was not confirmed).
- Retry success depends on the executor replay path returning success for an already-committed fact; consistent with source, unexecuted.
- All new tests are written, not executed.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
