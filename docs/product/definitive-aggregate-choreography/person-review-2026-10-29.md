# Person aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together.** One tenant-scoped person (`Person`, stream `people/{personId}`) decides, atomically, that a recorded workforce person has an immutable creation identity, revisioned contact values, and at most one current platform membership correlation. The values and the correlation share one revision, so they must be coherent at each revision. Another person and the roster do not share a stream. The stream holds a bounded header plus an immutable-content reference; values and history are external, and parent rosters are projections.

- Identity: tenant plus caller `PersonId`. `RecordPerson` uses `PersonId` as the immutable creation logical identity. Revise and correlate use `CommandId` plus the expected revision. A request key cannot fork a stream, because the stream address derives from the tenant and `PersonId` only.
- Invariant: one recorded person; revisioned contact content; at most one current membership correlation.
- Exclusions: membership authority and eligibility belong to Member. Access governance may correlate an observed account but never imports a competing roster (D22-02). Work relationships, locations and service identities are separate owners. The correlation never grants or revokes access.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record (valid) | created at revision 1 | `PersonRecorded` / native `PersonRegistrationRecorded` |
| none | Record (invalid) | validation refusal, no event | none |
| none | Revise / Correlate | missing record | none |
| created | Record, same normalized intent | original registration, no event | none |
| created | Record, changed intent | conflict, no event | none |
| created | Revise, expected = revision, valid | revision + 1 | `PersonRevised` / `PersonDetailsRevised` |
| created | Revise, stale or invalid | version conflict or invalid content, no event | none |
| created | Correlate, expected = revision, different user or clear | revision + 1 | `PersonMembershipCorrelated` / `PersonMembershipCorrelationRecorded` |
| created | Correlate, same link | no-op, no event | none |
| created | Correlate, empty user id or stale | invalid content or version conflict, no event | none |

There is no terminal state in the catalog. Lifecycle facts beyond generic revision are an open catalog item (`OPEN_LifecycleFactsOtherInventories`, Person 0.65); they are not implemented or invented here.

## Changes

No production change. Qualification against the done criteria found no behavioral defect in `Person` or `PersonRules`.

- Replay guards. Legacy facts cannot follow native content authority (`ObserveLegacy`). Native facts are checked for tenant, person, operation root, revision and correlation transition (`PersonHistory.NextNative`). The earlier Jev review's values-hash guard on correlation is present.
- Retry of a committed fact after later facts is handled in `PersonRules.Record`, which retains the initial values. This had no direct test.
- Exact retry of revise and correlate after the expected revision is consumed is handled by native operation receipts and is covered by `PersonNativeIdentityTests` and `PersonNativeHttpTests`.
- Stale revision and competing appends are decided on the single person revision (`PersonRules`) and the prepared prefix/metadata cut (`Person.Append`, `PersonNativeHistoryTests`).

Coverage gaps closed (tests only, in `PersonTests.cs`):

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Creation identity, exact retry, changed intent | `ShouldReplayIdenticalRecordAndRejectChangedContentGivenRecordedPerson` (existing) |
| Retry of committed creation after later facts | `ShouldReturnOriginalRegistrationGivenCreationRetriedAfterLaterFacts` (new) |
| Valid replay restores revision and correlation | `ShouldRestoreRevisionAndCorrelationGivenValidHistoryReplay` (new); native replay in `PersonNativeHistoryTests` (existing) |
| Stale revision, missing record | `ShouldAdvanceRevisionAndRejectStaleOrMissingGivenRevise`, `ShouldRejectCorrelationGivenStaleRevisionOrMissingPerson` (existing) |
| Revision does not disturb correlation or contact | `ShouldKeepCorrelationAndPersonalContactGivenRevisionWithoutContact` (new) |
| At most one current correlation (replace) | `ShouldReplaceCorrelationGivenDifferentUserAtCurrentRevision` (new) |
| Correlate and clear, same-link no-op | `ShouldCorrelateAndClearMembershipGivenCurrentRevision` (existing) |
| No-op clear of never-correlated person | `ShouldAppendNothingGivenClearOfNeverCorrelatedPerson` (new) |
| Invalid decisions append nothing | `ShouldRejectBeforeAppendGivenInvalidPerson`, `ShouldRejectGivenOversizeDisplayName` (existing); `ShouldAppendNothingGivenEmptyCorrelationOrInvalidRevision` (new) |
| Normalization and 200-character boundary | `ShouldNormalizeBlankContactAndAcceptBoundaryNameGivenRecordRetry` (new) |
| Restricted contact recorded | `ShouldRecordRestrictedPersonalContactGivenValidPerson` (existing) |
| Native identity, original receipt replay, prefix/metadata cut, reauthorization, legacy read | `PersonNativeIdentityTests`, `PersonNativeHttpTests`, `PersonNativeHistoryTests` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

- Legacy facts `PersonRecorded`, `PersonRevised` and `PersonMembershipCorrelated` are read-only history and still replay. Native facts `PersonRegistrationRecorded`, `PersonDetailsRevised` and `PersonMembershipCorrelationRecorded` carry a bounded header, content references and operation provenance.
- No payload, rename or discriminator change is needed. No event changes are requested.
- Open decision, unchanged: a revise whose values equal the current values still raises a revision fact (native history requires `Changed` for `RevisePerson`). The catalog gives no no-op rule for revise, so none is invented.

## Later-stage follow-ups (not aggregate gaps)

- Handler, reactor and projector work for correlation: Member and access governance decide eligibility before calling `CorrelatePersonMembership`.
- Directory projection (`PersonDirectoryProjector`) is a projection of this stream.
- The distinct person lifecycle facts decision stays an open catalog item.

## Evidence

- Jev: no new request this round. The earlier retained review is `jev-review-2026-10-27-person` (1.14.0; 0.87 correlation must not change values, 0.39 boundary, inconclusive). The boundary rests on the catalog and code, not on a score.
- Tests: written, not executed; build and run pending. Intended commands: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~PersonTests"`, then the backend unit suite.
- RED: none captured. The new tests cover behavior already implemented, so no failing run is expected; this is not a claimed red.
- No host, durable-store or production qualification is claimed.

## Review before build (independent, 2026-10-29)

Reviewer read the catalog entry, `Person.cs`, `PersonRules.cs` and `PersonTests.cs` directly. Nothing was built or run.

### Findings

- No behavioral defect. Every new test asserts events appended or not, or replayed state (revision, correlation, contact), and each can fail if the rule changes. Traced each against `PersonRules`: retry-after-later-facts, replay (revision 3, stale at 2, success at 3 gives revision 4 and keeps correlation via `with`), revise keeping contact and correlation, replacement, clear-of-never-correlated no-op, empty user id and invalid revise appending nothing, blank contact normalization and 200-character boundary.
- Compile read: overloads resolve by arity (`Revise` 5 vs 6 arguments, `Record` 4 vs 5); `PersonalContactDetails(string?, string?)` accepts `(" ", null)`; `Uuid.Empty`, `CommandFailureCode.InvalidContent` and `CommandFailure.InvalidContent` exist; `(Uuid?)` casts and the collection-expression `Assert.Equal` pattern match the already-compiling existing tests. Names follow CNTRYL0001 and every test has ordered Arrange/Act/Assert comments. No malformed-history test.
- No fixture value is validated by a type that could throw before the aggregate (only free-text names, emails and fresh Uuids).
- Could not confirm: that `Assert.Equal([first, second], IEnumerable<Uuid>)` binds without ambiguity (same shape as an existing test, so low risk); `ShouldRestoreRevisionAndCorrelationGivenValidHistoryReplay` reads `Current!.CorrelatedUserId` (nullable flow is fine under `!`).
- Observation, not a defect: a stale exact retry of a correlate that already committed returns a version conflict rather than an acknowledgment. This matches the catalog's declared identity ("exact successful revision retry becomes stale"); native receipts cover replay at the native layer.

### Fixes

None needed; no source or test edits.

### Jev (jev-1.14.0, advisory), files in `implementation-jev/person-test-impl-review-2026-10-29-{request,response}.json`

| Question | Answer |
|---|---|
| Replacing correlation with a different user at current revision is consistent with at-most-one correlation | Noul 0.94 |
| Creation retry after later facts acknowledged without fact, changed values refused, consistent with immutable creation identity | Noul 0.93 |
| Revise with unchanged values: append_revision / noop_success / needs_source_resolution | append_revision 0.93 (confidence 0.89), noop 0.05, needs_source 0.01 |
| Premise: the model is silent on revise-with-unchanged-values | Noul 0.63 (moderate, not decisive) |

### Discrepancy disposition

- Revise with unchanged values appends a revision fact; catalog has no no-op rule. Jev leans decisively to keep the current behavior (0.93), but its premise check is only 0.63. Disposition: keep current behavior, no code change; the catalog open item (`OPEN_LifecycleFactsOtherInventories`, Person 0.65) stays flagged as unresolved and needs an owner decision if a no-op rule is wanted. No event, payload or name change is requested.

### Remaining risks

- Tests and implementation written, not executed. Build and run pending.
- Revise-unchanged rule remains a catalog-level open decision.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
