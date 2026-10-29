# EvidenceSupport aggregate review

## Identity, invariant and boundary

- **Identity:** stream `evidence-support` under the tenant, keyed by the support id. `IdFor(LinkEvidenceSupport)` returns the authored `CommandId` (the route carries `{command_id}`), so no other caller key forks the stream. The catalog's `selectedIdentitySummary` still describes a UUIDv5 tuple (tenant + program/artifact/type/target/revision/period) as the owner identity with `CommandId` binding intent; the source (and `AdvisorRecordVersionResolver`, which compares `IdFor(request)` to the stored support id) uses `CommandId`. Left unchanged: changing the key would fork occupied streams. Recorded as unresolved.
- **Named invariant:** one relationship identity immutably binds exact artifact content/revision, target revision and covered period with an attributed sufficiency rationale.
- **Exclusions:** artifact lifecycle (`EvidenceArtifact`), target lifecycle (controls, occurrences, evaluations, periods), validation invalidation on supersession, rosters and views (projections).
- **Decision:** keep as one aggregate; a single immutable creation fact.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | `Link` at expected revision 0 | `EvidenceSupportLinked` | stale revision (VersionConflict); wrong tenant/program/identity, bad type, target id or revision, bad period, blank or oversize text, malformed hash, non-member or blank-display actor, default instant (InvalidContent) |
| Linked | `Link` exact retry (same intent, expected revision, member) | none; original frozen view returned even if artifact facts moved on | different intent, expected revision or actor (StateConflict) |

## Changes (real gaps)

- An actor whose display was whitespace passed the length check (`Display.Length > 0`), then the trimmed display failed `Apply` and threw `InvalidOperationException` instead of a refusal. The command now refuses a blank or oversize (after trim) display as InvalidContent.

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity | `ShouldRecordExactAttributableSupportRelationshipGivenValidLink`, `ShouldRejectOtherTenantOrIdentityWithoutEventGivenLink` |
| Invalid decision appends nothing | revision/period theory, `ShouldRejectWhitespaceActorDisplayWithoutEventGivenLink`, non-member actor, source/text theory, malformed hash, other tenant/identity |
| No-op / exact retry | `ShouldAcceptExactReplayAndRejectChangedRelationshipGivenStableSupportIdentity`, `ShouldReturnOriginalViewGivenRetryWithLaterArtifactFacts` |
| Changed intent | same, `ShouldRefuseChangedActorOrExpectedRevisionGivenCommittedSupport` |
| Stale revision | `ShouldRefuseStaleRevisionWithoutEventGivenNewSupport` |
| Replay | `ShouldRestoreSupportAndRefuseChangedIntentGivenValidHistory` |
| Bounded history | one immutable fact, revision 1 |

## Event contract status

`EvidenceSupportLinked` carries business facts only (source binding, period, rationale, actor, intent digest); no metadata duplication. No event change needed.

## Later-stage follow-ups (not aggregate gaps)

- Handler: member actors must have UUID ids and non-blank display.
- Catalog identity wording (tuple UUIDv5 versus `CommandId`) to be reconciled by the catalog owner.
- Supersession invalidation of validations belongs to the validating owner or a reactor.

## Evidence

Tests written, not executed; build and run pending. Nothing was built, formatted or committed.

## Review before build

Reviewer: independent skeptic pass, 2026-10-29. Nothing built, run, formatted or committed.

### Findings

- Implementation re-read against the invariant: tenant/program/identity, source revision, hash, type, period, text bounds, member actor and display are all validated before the single `EvidenceSupportLinked`; retry is decided before mutable artifact facts are read; stale revision is checked after retry recognition so a committed retry never reports stale. No wrong or duplicate fact path found. The payload repeats no metadata (TenantId/SubjectId are the base event shape listed in the catalog).
- Test gap fixed: oversize rationale (over 2000 after trim) and oversize actor display (over 200) had no test. Added `ShouldRejectOversizeRationaleOrActorDisplayWithoutEventGivenLink`.
- Existing tests all assert events appended or not and state; the whitespace-display test fails if the aggregate throws or accepts. Names and Arrange/Act/Assert phases conform. Fixtures use a UUID member id, lowercase 64-hex hash, supported types.
- Compile read: the extended property pattern plus a following `string.IsNullOrWhiteSpace(actor.Display)` is nullable-safe (Display is non-nullable string). `ActorReference` is a public three-argument record, so `new ActorReference("service", ...)` compiles. xUnit widens int InlineData to the long parameter. `NativeFacts.Raised/Replay` are used correctly and state is captured before further commands.

### Jev (jev-1.14.0, advisory), saved in implementation-jev/evidencesupport-test-impl-review-2026-10-29-*.json

| Question | Answer | Probability |
|---|---|---|
| Identity: CommandId in code vs catalog UUIDv5 tuple | keep CommandId, flag catalog | 0.96 (switch 0.00) |
| Premise: the two describe different keys | true | 0.90 |
| Retry after artifact facts moved | success, no event, original view | 0.79 (conflict 0.20; confidence 0.69) |
| Payload TenantId/SubjectId | keep as cataloged | 0.99 |
| Whitespace-actor test asserts invariant | true | 0.73 |

Dispositions: identity wording is decisive (0.96) so code stays; the catalog correction is for the catalog owner (not edited). Retry outcome has a clear lead (0.79) and matches the code. Payload kept.

### Remaining risks

- Catalog `declaredIdentity` (UUIDv5 tuple) needs owner reconciliation to CommandId; a distinct CommandId with identical relationship facts creates a second support stream under the current key (the catalog tuple would have deduplicated it).
- Tests written, not executed.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
