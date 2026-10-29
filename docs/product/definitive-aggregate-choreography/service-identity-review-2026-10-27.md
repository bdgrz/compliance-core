# ServiceIdentity aggregate review

Initial review 2026-10-27; re-verified against the 2026-10-29 catalog (aggregate-details.md row `ServiceIdentity`, decisions `OPEN_LifecycleFactsOtherInventories`).

## Identity, invariant, exclusions

- **Identity:** tenant + caller `ServiceIdentityId`, stream `service-identities/<id>`. The identity is the governed non-human identity itself, not a request key; a retried create reuses the same stream. Revisions are ordered by expected revision. Catalog says revisions use caller `CommandId`; command identity and dedupe are framework metadata and are not carried in the payload.
- **Named invariant (M0-D06, D22-03):** one governed non-human identity has exactly one accountable person or team owner (never free text, group or role), an approved purpose, identity kind in workload/service/automation/bot/integration, lifecycle active/disabled/retired, an active identity is not already expired, and the review date is at most one year out (and in the future unless retired).
- **Exclusions:** existence and liveness of the owner person/team (checked by the handler against authoritative workforce owners), shared-account justification, unowned-work explanation (`ServiceIdentityAccountability`, a pure rule), directory reads (projector).

## Boundary decision

Keep together: one stream per identity, all terms are decided atomically against one owner. History is bounded by revisions of a single identity (low contention, no roster). Distinct lifecycle facts (disable/retire) remain an open catalog decision (0.67); generic revision facts stay, so no split or new event was introduced.

## Lifecycle

| State | Command | Result |
| --- | --- | --- |
| absent | Record(valid terms) | `ServiceIdentityRecorded`, revision 1 |
| absent | Revise | missing-record refusal, no fact |
| created | Record, same canonical terms as the original | success, no fact |
| created | Record, different terms | conflict, no fact |
| created | Revise, stale expected revision | version conflict, no fact |
| created | Revise, invalid terms | invalid content, no fact |
| created | Revise, canonical terms equal current terms | no-op, no fact (new) |
| created | Revise, changed valid terms (any status incl. disabled/retired) | `ServiceIdentityRevised`, revision + 1 |

## Changes this round

- Gap: a revision with terms identical to the current terms appended a redundant `ServiceIdentityRevised`. The aggregate now tracks current terms and treats an unchanged revision at the current revision as a no-op.
- Replay guards (tenant, identity, single create, contiguous revision, canonical valid terms) were already present and are unchanged. No malformed-history tests, per user preference.

## Test to criterion

| Criterion | Test |
| --- | --- |
| Accepted create | ShouldRecordIdentityGivenOwnerPurposeAndReviewWithinOneYear, ShouldRecordExpiryGivenFutureExpiryDate |
| Invalid create, no fact | ShouldRejectRecordGivenReviewDateOutsideNextYear, ShouldRejectRecordGivenNonAccountableOwnerOrMissingPurpose, ShouldRejectRecordGivenActiveIdentityAlreadyExpired |
| Exact retry vs changed intent | ShouldReplayIdenticalRecordAndConflictGivenDifferentContent |
| Retry of committed create after later facts | ShouldAcceptRecordRetryGivenLaterRevision (new) |
| Revise, stale revision, missing | ShouldReviseOwnerAndRejectStaleGivenRecordedIdentity |
| Invalid / stale revise appends nothing | ShouldAppendNoFactGivenInvalidRevisionOrMissingRecord (new) |
| No-op revise | ShouldAppendNothingGivenRevisionWithUnchangedTerms (new) |
| Valid replay / hydration continues | ShouldContinueFromReplayedStateGivenRecordedAndRevisedHistory (new) |
| Accountability and expiry rules | ShouldExplainUnownedWorkGivenOwnerAndReviewState, ShouldReportExpiredGivenExpiryDateReached |

Competing appends: both writers read the same revision; the framework's optimistic concurrency rejects the second append and the retry re-decides through the stale-revision check above. No aggregate-level test beyond that.

## Event contract status

`bdgrz.workforce.service-identity.recorded` v1 and `...revised` v1 carry tenant, identity id, terms, actor and changed-at. `TenantId`, `ServiceIdentityId` and `ChangedAt` duplicate stream and envelope data but are persisted wire contracts the reducer validates; changing them needs a versioned contract change and is not proposed. `Revision` in the revised fact is a business sequence. No event changes needed.

## Later-stage follow-ups

- Handler: owner existence/liveness check and shared-account justification; mapping caller `CommandId` to framework command identity so retry of a committed revision after later facts returns the original result (the aggregate cannot recognise it from stale revision alone).
- Reactor/projector: unowned-work explanation and directory projection when owner leaves or team is deleted.
- Catalog: distinct disable/retire facts remain open (0.67).

## Evidence

Tests are written, not executed; build and run pending. Source edit is small (one tracked field, one no-op branch). Earlier focused run (2026-10-27, 44 passed with NamedRecordProcessTests) predates these changes. No Jev call was made this round; no event name changed.

## Review before build (2026-10-29)

Independent review of the source and tests before any build. Nothing compiled or executed.

### Findings and fixes

- **Record retry validated before recognising the retry (fixed).** An exact retry of a committed Record after the review date elapsed would have returned a validation error instead of an acknowledgement. `Record` now acknowledges canonical terms equal to the recorded terms first (null-safe guard on display name and purpose), then validates, then conflicts on different terms. Event raised from `Clean(terms)`.
- **Misnamed test (fixed).** `ShouldAppendNoFactGivenInvalidRevisionOrMissingRecord` is now `ShouldAppendNoFactGivenInvalidStaleOrMissingRevision` and also covers the missing-record refusal with no pending events.
- **New test:** `ShouldAcknowledgeRecordRetryGivenReviewDateElapsedSinceCommit` (retry on a later date, success, one event only).
- **Replay test hardening:** `Given` now receives an explicit `DomainEvent[]` to avoid overload risk.
- No payload field was added or changed; no event changes needed. Duplicated metadata fields (tenant, id, changed-at) are existing wire contracts, unchanged.

### Jev (jev-1.14.0, advisory), request/response in `implementation-jev/serviceidentity-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability |
| --- | --- | --- |
| No-op revise with unchanged terms is consistent with the rule | true | 0.94 |
| Identical Record retry after review date passed | acknowledge_without_event | 1.00 |
| Test scenarios assert behavioral outcomes | true | 0.97 |
| Revise retry via CommandId: add_to_model / handler_framework_identity / needs_source_resolution | add_to_model | 0.53 / 0.31 / 0.16 (confidence 0.30) |

The first three were applied. The CommandId discrepancy (catalog says revisions use caller CommandId; the aggregate recognises retries only by expected revision plus unchanged terms) is near-even and low confidence: left UNRESOLVED. Not applied; adding a command id to the payload would be a shared event/schema change.

### Remaining risks

- Unconfirmed by compiler: `terms is { DisplayName: not null, Purpose: not null }` pattern on a non-nullable record parameter; `DomainEvent` and `DomainEventSeed.Attach(fact, id, version)` resolution in the test; `AggregateScenario.Given(DomainEvent[])`.
- Revision retry after later facts still depends on the CommandId decision above.
- Distinct disable/retire facts remain open (0.67).
- Tests written, not executed.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
