# EvidenceRequest aggregate review

## Identity, invariant and boundary

- **Identity:** `AggregateIdentity.Create("EvidenceRequest", tenantId, programId, requestId)` on stream area `evidence-requests`, realm = tenant. `EvidenceRequestId` is the caller-supplied immutable creation identity (catalog); an exact retry resolves the original result through `NativeAcceptance` and a different id is a different record. Later decisions are bound to this identity by command id (logical operation) and the expected revision; no caller key forks the stream.
- **Named invariant:** one open-to-fulfilled/cancelled lifecycle; exactly one terminal transition, decided at the expected revision. A fulfilled request can be held (revision unchanged) by a risk action completion review through `SourceUseReservations`; while a hold is active a conflicting write is refused until it settles.
- **Exclusions:** artifact capture and its content (`EvidenceArtifact`), control linkage and owner membership checks (handler/guard, read-side), request rosters and due-date views (projections), risk action completion review decisions (`RiskTreatmentAction` via the two reactors).
- **Decision:** keep as one aggregate. Opening, fulfilment, cancellation and the source-use hold are one lifecycle decided atomically; history is bounded (one open, at most one terminal fact, plus technical hold facts handled by the verified selection, not retained in state).

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | `OpenRequest` | `EvidenceRequestOpened` | blank title or instructions, no owner, due date in the past (Validation); own-operation mismatch (Validation) |
| open | `Fulfil` at revision 1 | `EvidenceRequestFulfilled` (rev 2) | empty artifact (InvalidContent); stale revision, active hold (Conflict); not opened (NotFound) |
| open | `Cancel` at revision 1 | `EvidenceRequestCancelled` (rev 2) | blank rationale (InvalidContent); stale revision, active hold (Conflict) |
| fulfilled / cancelled | `Fulfil` / `Cancel` | none | already decided (StateConflict) |
| any | exact retry of a committed operation | none | `NativeAcceptance.Decide` returns Replayed, even after later facts; changed intent returns Conflict |
| fulfilled | `AuthorizeActionCompletionUse` / `SettleActionCompletionUse` | `...UseAuthorized` / `...UseSettled` (revision unchanged) | open request or foreign stream (Conflict) |

## Changes (real gaps)

- `OpenRequest`, `Cancel` and `Fulfil` relied entirely on the handler for content validation; the aggregate accepted a blank title or instructions, a past due date, a blank cancellation rationale and an empty artifact id. They now apply the same `EvidenceRequestRules.ValidateOpen` / `ValidateCancel` the handlers use, and refuse an empty artifact id, before any event is raised.

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity | covered by `AggregateIdentity` derivation in `EvidenceRequest.IdentityOf`; open-retry via `ShouldReplayIdenticalOpenGivenSameRequestId` |
| Success paths | `ShouldOpenAnAttributedRequestGivenValidDetails`, `ShouldFulfilWithAnArtifactGivenTheCurrentRevision` |
| Invalid decision appends nothing | `ShouldRejectBeforeAppendGivenInvalidDetails`, `ShouldRefuseOpeningGivenBlankTitleAtTheAggregate`, `ShouldRefuseCancellationGivenBlankRationaleAtTheAggregate`, `ShouldRefuseFulfilmentGivenNoArtifactAtTheAggregate`, `ShouldReportMissingRecordGivenUnknownRequest`, `ShouldRefuseActionCompletionUseGivenOpenRequest` |
| No-op / exact retry after later facts | `ShouldReplayIdenticalOpenGivenSameRequestId`, `ShouldReplayCommittedDecisionAndConflictChangedIntentGivenReplayedHistory` |
| Changed intent | `ShouldReplayCommittedDecisionAndConflictChangedIntentGivenReplayedHistory` |
| Exactly one terminal transition | `ShouldRefuseFulfilmentGivenCancelledRequest`, `ShouldRefuseCancellationGivenFulfilledRequest` |
| Stale revision / competing | `ShouldRejectStaleRevisionGivenConcurrentChange`; concurrent open admission in `EvidenceRequestNativePreparationTests` |
| Replay of valid history | `ShouldRestoreCancelledRequestGivenValidHistory`, `ShouldKeepOnlyLifecycleContentAndSelectedFenceGivenLongSettledUseHistory` |
| Bounded history | `ShouldKeepOnlyLifecycleContentAndSelectedFenceGivenLongSettledUseHistory` (128 settled uses, no retained collection) |

## Event contract status

`EvidenceRequestOpened`, `Fulfilled`, `Cancelled`, `ActionCompletionUseAuthorized` and `ActionCompletionUseSettled` carry business facts and operation provenance only; no metadata duplication. No event change needed.

## Later-stage follow-ups (not aggregate gaps)

- Handlers keep validating before dispatch (owner membership, control existence, normalization); the aggregate guard is a second line of defence with the same rules.
- The two risk action completion reactors own delivery and ordering of hold and settle commands.
- Request rosters and overdue views are projections.

## Evidence

Tests written, not executed; build and run pending. Source guards were written with their tests (RED not run). Nothing was built, formatted or committed. Risk: a handler or process test that opens with a due date earlier than the clock at the aggregate call, or cancels with a blank rationale directly on the aggregate, would now be refused. Jev was not consulted: no event or name changed.

## Review before build

Independent review of source, tests and review file; nothing built or run.

**Findings and fixes**

- Fixed: `ShouldReplayCommittedDecisionAndConflictChangedIntentGivenReplayedHistory` bound acceptance to the locally built `decision` provenance, which has not been through the JSON round trip; it now uses `restored.Acceptance.SelectedProvenance`, the replayed original (same pattern as the ControlEvaluationPlan replay test), so it asserts what replay actually restored.
- Confirmed: guards run before `PrepareOwnDecision`, so an invalid decision on an already-decided request reports InvalidContent rather than StateConflict; both append nothing. Accepted.
- Confirmed: every test has ordered Arrange/Act/Assert comments, `PendingEvents.Count` is used as a property, and the replay tests seed through `NativeFacts.Raised`/`Replay` (no `DomainEventSeed.Attach` on pending events). The added `using Bdgrz.Compliance.Tests.Testing;` and `Evidence.Rules` using exist.
- Remaining risk (not fixed): the aggregate validates trimmed title/instructions but stores `terms` as given; handlers pass normalized terms, so no divergence in production. Seeds (`EvidenceSeeds.OpenAsync`) now run `ValidateOpen`; callers use `due >= openedAt` today, but a seed with a due date before `at` would now be refused. Not compile-verified.
- Not added: blocking a decision during an active hold (needs verified selection plumbing), as the implementer noted.

**Jev (model jev-1.14.0, advisory)** — request/response in `implementation-jev/evidencerequest-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability |
|---|---|---|
| Cancel after fulfil: Conflict, no event? | conflict_no_event | 1.00 |
| Content guards in aggregate as well as handler? | aggregate_and_handler | 0.86 (confidence 0.79) |
| Replay test asserts retry-ack and changed-intent conflict? | noul | 0.94 |

**Discrepancies:** none. No event, name or payload differs from the catalog; no catalog change needed.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
