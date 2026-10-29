# ApplicationRelationship aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (one stream per directed tuple).** `ApplicationRelationship` (stream `application-relationships/{relationshipId}` under the tenant) decides one exact directed source / kind / target relationship lifecycle.

- Identity: `relationshipId` = `ApplicationRelationshipIdentity.Id(source, kind, target)` (UUIDv5, unchanged). The aggregate refuses any add whose stream id differs from the tuple id and `Apply` re-checks it, so no caller key forks a stream. No separate claim/release owner (EV-D03/EV-D04).
- Invariant: one relationship exists once per tuple; it is created at revision 1 with status `active` (`depends_on`) or `proposed` (`replaces`). Endpoints are nonempty, distinct, and the kind is supported.
- Exclusions: active-endpoint and source-visibility checks, exact source/target application revision guards (DeclaredApplication owns application revision), successor approval and removal authority (still on DeclaredApplication), relationship lists/history (projections), authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Add (expected revision 0, canonical tuple id, supported kind, distinct endpoints, own-stream root operation) | active or proposed | `ApplicationRelationshipAdded` |
| exists | Add (same or other intent) | conflict, no fact (exact retry resolves in the operation layer) | none |
| none | Add with expected revision != 0 | conflict, no fact | none |
| any | non-canonical id, unsupported kind, empty or self endpoints, bad actor | validation, no fact | none |
| active/proposed | Approve successor, Remove | NOT on this aggregate yet (see open item) | `ApplicationRelationshipApproved`, `ApplicationRelationshipRemoved` |

## Changes

Real gap: `PrepareAdd` and the `Apply` guard accepted unsupported relationship kinds, empty endpoints and self relationships (the DeclaredApplication path refuses all three). Added those checks to both `PrepareAdd` and `Apply`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity (no caller key forks a stream) | `ShouldRejectNonCanonicalIdentityGivenRelationshipTuple` |
| Valid creation, both kinds, event contract | `ShouldAppendCanonicalRelationshipGivenValidRootAddition` |
| Invalid decision appends nothing: self relationship | `ShouldRejectSelfRelationshipGivenSameSourceAndTarget` (new) |
| Invalid decision appends nothing: unsupported kind | `ShouldRejectUnsupportedTypeGivenCanonicalTupleIdentity` (new) |
| Retry after commit / stale revision, no second fact | `ShouldRefuseSecondAddWithoutFactGivenExistingRelationship` (new) |
| Replay of valid history, later command refused | `ShouldRestoreRelationshipStateGivenValidHistoryReplay` (new) |
| Source-side tuple uniqueness, removal tombstone, independent expected revision, approval replay | existing `Tests/ApplicationRelationshipTests.cs` (DeclaredApplication path) |

Malformed-history tests were not added, by user preference. Changed-intent refusal on the same id is structurally impossible here (id derives from the intent); the exact-retry result is resolved by the operation layer.

## Event contract status

- `ApplicationRelationshipAdded` matches the catalog (`bdgrz.application.relationship.added` v1). No add, rename, remove or payload change requested.
- Catalog `preliminaryNewEvents` (`...IdentityClaimed/Released`) are superseded by EV-D03/EV-D04; none exist or are needed.
- `ApplicationRelationshipApproved` and `ApplicationRelationshipRemoved` are catalogued for this owner but are still folded by DeclaredApplication (source stream, plus `ApplicationRelationshipRecorded`/`GovernedRemoved` variants); not moved.

## Open item (aggregate is partial)

Approval and removal are not implemented on this stream. Moving them needs a decided coordination design for the exact source/target application revision guards and retirement-requires-approved-successor rule (catalog status: implementation halted, separate verification required). Not guessed here.

## Later-stage follow-ups (not aggregate gaps)

- Handler wiring of `AddApplicationRelationship` to this stream; projectors for outgoing/incoming relationship lists; authorization precedence before replay.

## Evidence

- Jev: no request this round; catalog and code were sufficient, no event name in question.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ApplicationRelationshipTests"`.
- RED: not captured, not claimed. The self and unsupported-kind tests were expected to fail before the fix.
- Residual risk: uncompiled.

## Review before build

Findings:
- Test weakness (fixed): `ShouldRefuseSecondAddWithoutFactGivenExistingRelationship` drove the stale call through `Add` with expected revision 1, which fails earlier on the operation-provenance check (Validation) and asserted only `IsSuccess == false`. It now calls `PrepareAdd(1, ...)` and asserts `Conflict`, so the stale-revision rule is actually exercised.
- Implementation: no correctness gap found. `Add` refuses a second add (Conflict, no event); `Apply` re-checks tuple identity, endpoints, kind, own-stream operation; no payload field duplicates metadata.
- Compile review: usings present (`Bdgrz.Compliance.Tests.Testing` for NativeFacts), `Assert.Single`, `!` on `Error`, no `.Count()`. Not compiled; unconfirmed.

Jev (model jev-1.14.0; requests/responses in `implementation-jev/applicationrelationship-test-impl-review-2026-10-29-*.json`):
- Second Add at aggregate = Conflict, exact-retry ack in operation layer: conflict_no_event 1.00. Applied (no change).
- Approve/Remove: leave_partial 0.95. Applied (kept partial).
- Tests assert behavior and can fail: noul 0.93.

Discrepancies: none new (no event add/rename/payload change). Remaining risk: uncompiled; Approve/Remove open.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
