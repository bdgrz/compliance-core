# SystemDescription aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (selected split target, retained as one owner).** One description (`SystemDescription`, stream `system-descriptions/{descriptionId}` under the tenant) decides atomically its immutable narrative version line, review and approval against the exact source snapshot.

- Identity: `(tenantId, programId, descriptionId)`. `DescriptionId` on `CreateSystemDescriptionDraft` is the caller-supplied immutable creation identity, so an exact retry resolves the original result and a different id is a different record. Later mutations use `CommandId` bound to this identity and the expected revision. No caller key forks the stream.
- Invariant: immutable narrative versions, completeness and approval against an exact source snapshot; no approved content or source snapshot mutates; exact changes and period decisions are admitted before approval.
- Exclusions: export generation (`SystemDescriptionExportProcess`), the source records themselves (boundary, control, etc.), examination ownership, projections and reads, authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Create (own id, revision 1, version 1) | draft | `SystemDescriptionDraftCreated` |
| draft | Revise / BindSources (expected revision = current) | next version | `SystemDescriptionDraftRevised` / `SystemDescriptionSourcesBound` |
| draft | Review (non-author, exact current version) | review recorded | `SystemDescriptionReviewed` |
| draft | Approve (non-author, reviewed, exact source proof) | approved | `SystemDescriptionApproved` |
| draft | NoChangeDecision (period decision) | decision recorded | `SystemDescriptionNoChangeRecorded` |
| approved | ProposeSuccessor | next draft version | `SystemDescriptionSuccessorProposed` |
| approved | BindToExamination | binding | `SystemDescriptionBound` |
| approved | Revise / BindSources | refused | none |
| any | stale expected revision, changed intent, reused version id | conflict | none |

## Changes

None to the aggregate. Verification against the polished catalog found the source consistent: eight facts match the catalog event list, `Append` fences stale sequence and physical position as a transient conflict, `Validate` binds tenant, program, stream identity, revision gate, lineage, actor and operation provenance, and compact facts reference immutable content (bounded history). Gaps were test coverage only.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Exact retry after later facts, original result | `ShouldReplayOriginalReviewGivenLaterNarrativeRevisionAndExactAuthorizedRetry`, `ShouldReplayOriginalRevisionAndRefuseChangedIntentGivenExactRetryAfterLaterRevision` (new) |
| Changed-intent refusal | same two tests |
| Unknown append outcome, no duplicate fact | `ShouldRecoverOriginalReviewWithoutDuplicateFactGivenUnknownAppendOutcome` |
| Stale revision, no fact | `ShouldRejectStaleDraftRevisionWithoutAppendingGivenPersonalCommand`; competing decision `ShouldRefuseSecondRevisionWithoutFactGivenCompetingDecisionAtSameExpectedRevision` (new) |
| Approved content immutable, no fact | `ShouldRefuseRevisionWithoutFactGivenApprovedDescriptionIsImmutable` (new) |
| Approval requires exact snapshot proof | `ShouldFenceUnqualifiedPersistedSourceHistoryGivenApprovalRequiresExactSnapshotProof` |
| Version identity cannot be reused | `ShouldRejectHistoricalVersionIdentityReuseGivenCurrentPreparationOmitsOldNarrative` |
| Replay and bounded history | `ShouldEditCurrentDraftWithoutLoadingHistoricalNarrativeGivenOlderSourceBodiesUnavailable`, `ShouldRetainCompleteNarrativeGivenValidContentExceedsOldEventEnvelope` |
| Event wire contract | `SystemDescriptionNativeVersionContractTests` |
| Lifecycle rules (completeness, independent review, period changes) | `SystemDescriptionTests` |

Malformed-history tests were not added, by user preference. The competing-decision test models the loser with a stale expected revision after the winner commits; true parallel append races are covered by the stream sequence and position fence in `Append` and by the unknown-outcome recovery test.

## Event contract status

Eight facts, each carrying header, content references, native sequence, change count and operation provenance as the compact native envelope. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handler wiring of authorization precedence before replay is already in `SystemDescriptionOperations`; any change belongs to the handler stage.
- Export process, snapshot source projections and examination binding reactions remain process/projector concerns.

## Evidence

- Jev: no request this round; the catalog entry and selected replay rule were sufficient.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SystemDescriptionNativeTests"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the exact-retry-of-revise assertion assumes the operation receipt replays by `CommandId` as it does for review; if it fails on first run, that is a real handler or executor gap to triage. Uncompiled.

## Review before build

Independent review of source, tests and review file; nothing compiled or run.

### Findings

- Implementation: `Append` fences native sequence and physical position, binds the fact to the frozen verified mutation, and `Validate` binds tenant, program, stream identity, revision gate, lineage, actor and operation provenance. Replay of committed commands is resolved by the `Acceptance` receipt before preparation, so exact retries after later facts append nothing. No defect found; no source change.
- Tests: the three new tests assert appended-fact counts and read-back state, not structure. Revise passes a null receipt, but receipt content only matters for review, so the retry test does not depend on it. Approved seed revision is 3 (create, review, approve); test 3 reads it instead of hard-coding. Stale and approved refusals map to `Conflict` through `CommandFailure`. Competing test models the loser as a stale expected revision (true parallel race is covered by the sequence and position fence).
- Compile read: `Revision(...)` helper is static in the test class and does not collide with locals; `request with { Title = ... }` on a sealed record is valid; `Assert.Equal(int, long)` is already used in this file; `.Count` on IReadOnlyList; `Error!` after `Assert.False`; usings already present. No risk found, not compiled.
- No test fix needed.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/systemdescription-test-impl-review-2026-10-29-{request,response}.json`.

| Question | Answer | Probability |
|---|---|---|
| Tests t1-t3 assert the replay rule and invariant | noul | 0.69 (moderate; unresolved, no gap identified on inspection) |
| Retry after later fact | success, no new fact; changed intent refused | 1.0 |
| Payload duplicates identity/metadata (premise) | noul | 0.98 |
| Duplicated payload fields | remove_from_payload | 0.79 (keep_as_is 0.21) |

### Discrepancy: payload fields repeating identity and metadata

All eight facts carry `TenantId` and `SystemDescriptionId` (equal to the stream address) and `OccurredAt` and `ActorMemberId` (equal to `OperationProvenance.AcceptedAt` and `Actor.Id`); `Validate` enforces the equality. Jev leans to remove (0.79, confidence 0.72, not near-even). This is a wire change to eight discriminated events and the generated contracts, so it is not applied here. Disposition: reported in remaining for the coordinator or owner; the aggregate keeps the validated binding until a wire-versioned event change is made.

### Remaining risks

- Tests written, not executed. If the exact-retry test fails, triage the operation receipt path, not the aggregate.
- Payload de-duplication above is an open event-contract change.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
