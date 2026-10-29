# CriteriaTextOverlay aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and readers are untouched. Catalog entry verified: `aggregates.json` owner `CriteriaTextOverlay` (selected split target, declared identity `(editionId, normalizedCriterionId)`, tenant is the stream realm), backlog group 66, product rule D02-06.

## Boundary decision

**Keep together.** One stream per tenant, edition and canonical criterion identifier. Identity is `AggregateIdentity.Create("CriteriaTextOverlay", tenant, edition, identifier)`; the command id (`CommandId`) is only the logical operation identity carried in provenance and never forks the stream.

Named invariant: one licensed text overlay per criterion is a sequential revision line; each revision is accepted only at the exact prior revision, references the complete supplied content as verified immutable content, and is a root operation of its own overlay.

Exclusions: the criterion catalog and edition text (never touched), cleaning and bounds of the supplied text, supplier, license and usage flags (`CriteriaTextOverlayRules`, applied by the handler before content is written), display/export usage policy (reader), the directory projection and cross-owner existence checks of the edition and criterion (handler admission).

History bound: state is only the latest fact; ProjectionTests and handler tests prove 32 revisions retain no history collection.

## Lifecycle

| State | Command | Result |
|---|---|---|
| revision 0 | Set, expected 0 | `CriteriaTextOverlayEntryRevised` revision 1 |
| revision n | Set, expected n | revision n+1 |
| revision n | Set, expected other | conflict, no fact |
| any | Set without expected revision or as another owner's root | validation, no fact |
| any | same CommandId, same intent | original registration (native acceptance in the handler), no fact |
| any | same CommandId, changed intent | conflict (handler), no fact |

## Changes

No production change. Qualification found no behavioral defect in the aggregate. The gap was missing owner-level coverage: identity, the revision guard and valid replay were proved only through handler and projection tests. A new test file adds them.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no caller key forks the stream | `ShouldDeriveStableIdentityGivenSameAndDifferentCriteria` (new, `CriteriaTextOverlayAggregateTests`) |
| Named invariant: sequential revision | `ShouldRaiseNextRevisionGivenExactExpectedRevision` (new) |
| Invalid decision appends nothing; stale and competing | `ShouldRefuseWithoutFactGivenStaleOrFutureExpectedRevision`, `ShouldRefuseWithoutFactGivenMissingExpectedRevisionForeignOwnerOrOtherCommandKind` (new); `ShouldRejectStaleRevisionWithoutWritingGivenOverlayChanged`, `ShouldAppendExactlyOneRevisionGivenTwoWritersAtTheSameExpectedRevision` (existing) |
| Exact retry after later facts; changed intent | `ShouldReturnOriginalRevisionGivenReplayAfterLaterRevision`, `ShouldConflictGivenSameCommandWithChangedIntentOrRevision` (existing) |
| Valid replay, then next revision | `ShouldReplayLineGivenValidRaisedHistoryAndAcceptNextRevision` (new) |
| Independent criteria | `ShouldKeepCriteriaIndependentGivenDifferentIdentifiersAtTheSameRevision` (existing) |
| Bounded history, complete content | `ShouldKeepOverlayAuthorityBoundedGivenManySuppliedTextRevisions`, `ShouldKeepCompleteMaximumUnicodeTextInImmutableContentGivenBoundaryRevision` (existing) |
| Source facts required | `ShouldRejectOverlayWithoutWritingGivenMissingOrInvalidSourceFacts` (existing) |
| Authorization | `CriteriaTextOverlayAuthorizationTests` (existing) |
| Projection | `CriteriaTextOverlayProjectionTests` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event changes needed. `CriteriaTextOverlayEntryRevised` (`bdgrz.criteria-overlay.entry-revised`, v2) matches the catalog's baseline event; `preliminaryNewEvents` is empty. The payload carries tenant, edition, identifier, revision, immutable content reference, operation provenance and first-response and result references; licensed text is held only in the referenced content, not in the event.

Observation only: `Set` relies on the handler to pass the `SetCriteriaTextOverlay` command kind; a different kind would throw in `Apply`. The handler always supplies it, so this is recorded, not changed.

## Later-stage follow-ups (not aggregate gaps)

- Reader consistency between the overlay directory projection and the stream.
- Ensure the platform-level content path refuses licensed text (D02-06 refusal is outside this owner).

## Evidence

Tests are written, not executed; build and run pending. No Jev request was made for this slice (no open design decision). Helpers used (`NativeFacts.Root`, `Ref`, `Raised`, `Replay`, `AggregateScenario`) were read but not compiled.

## Review before build

Findings and fixes:
- Defect (fixed): `Set` checked only owner and expected revision, so an operation with another command kind (or non-root shape) passed admission and then threw `InvalidOperationException` from `Apply`. `Set` now validates the whole root-operation shape with `NativeOwnerHistory.IsOwnRoot(this, operation, nameof(SetCriteriaTextOverlay), expected)` and returns a Validation refusal with no fact; `firstResponse` and `operationResult` get `ArgumentNullException.ThrowIfNull`. This supersedes the earlier "observation only" note above.
- Test (fixed): the foreign-owner/missing-revision test was extended (and renamed `...OrOtherCommandKind`) with a wrong-command-kind case asserting Validation and no pending event.
- Compile risk (fixed): removed the unused `Criteria.Events` using from the aggregate test file.
- Not confirmed (no build): `AggregateScenario` capturing pending events when constructed before acts (same pattern as `TeamRoleTests`), and Assert.Equal(int, long) generic inference.

Jev (model jev-1.14.0, request/response in `implementation-jev/criteriatextoverlay-test-impl-review-2026-10-29-*.json`):
- Stale/future test asserts no fact: Noul 0.92.
- Other command kind disposition: refuse_validation 1.00 (throw_on_apply 0, needs_source_resolution 0); premise (malformed operation returns refusal, not exception) Noul 0.87. Applied.
- Replay test continues the revision line: Noul 0.88.
No catalog or event discrepancy; no event changes needed.

Remaining risk: tests and the source change are written, not executed; build and run pending.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
