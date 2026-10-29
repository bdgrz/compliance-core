# ArtifactLegalHold aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run pending.

## Boundary decision

**Keep as a single owner (selected split from ArtifactRetention, retained).** One hold stream (`artifact-legal-holds/{holdId}` under the tenant realm) decides one indefinite placement over exact content and at most one exact release of that same content.

- Identity: tenant + caller-supplied immutable `HoldId` (logical id of the creating `PlaceArtifactLegalHold`); releases use `CommandId` bound to this hold and revision 1. No request key forks the stream; a different hold id is a different record, so holds on the same content do not contend.
- Invariant: one legal hold lifecycle and one exact release after placement. Placement at revision 0 binds exact source content and position; release at revision 1 must name the same source content, a source position not before the placement, and a time not before the placement. Nothing releases by timeout or missing read (`mustPreserve`): there is no expiry transition.
- Exclusions: source existence, content-digest check against the live source, actor authorization and personal-HTTP requirement, hold-versus-basis/disposition interplay (handlers, ArtifactRetentionBasis, ArtifactDisposition, snapshot projection); the active/history counts (projection).
- Alternatives: split further (no, two facts of one lifecycle); reactor (no cross-owner decision).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Place, own root operation, expected revision 0, valid source, bounded reason | placed, revision 1 | `ArtifactLegalHoldPlaced` |
| none | Place with other logical id, stream, kind, revision, tenant, bad source or reason | validation refusal | none |
| none | Release | not found | none |
| placed | Place, exact retry | original result | none |
| placed | Place, changed intent or new operation | conflict | none |
| placed | Release, same content, later-or-equal position and time, revision 1 | released, revision 2 | `ArtifactLegalHoldReleased` |
| placed | Release, different content/source, earlier position | conflict | none |
| placed | Release, wrong revision, before placement, empty reason, unselected command id | validation refusal | none |
| released | Release, exact retry of the committed release | original result | none |
| released | Release, new command id | conflict | none |
| released | Place, exact retry after the release | original result | none |

No edit or expiry transition exists in the catalog; none was invented.

## Changes (real gaps)

- `Place` ran the native replay decision before checking the logical id. `NativeAcceptance.Decide` throws `ArgumentException` for a binding whose logical id is not the hydration's selected one, so a placement rooted in another id (or on an unselected owner) threw instead of refusing. It now refuses with a validation error first, appending nothing. `ReleaseHold` has the same guard against its selected command id. Handler behavior is unchanged: the handlers select `HoldId` for place and `CommandId` for release.
- `Place` and `ReleaseHold` did not guard null source or null response refs; a null ref raised a fact that `Apply` then refused. They now throw `ArgumentNullException` before any fact is raised.

## Test-to-criterion table

New owner-level file `ArtifactLegalHoldTests`; the end-to-end cases in `ArtifactRetentionTests` and handler tests are retained.

| Criterion | Test |
|---|---|
| Stable identity, no key forks stream | `ShouldKeepStreamOfHoldGivenTenantAndHoldIdsOnly` |
| Valid placement, payload facts, trimmed reason | `ShouldPlaceRevisionOneHoldGivenValidRootOperation` |
| Invalid placement appends nothing | `ShouldAppendNothingGivenPlacementNotRootedInHoldOrMissingBusinessFacts` |
| Null refs refused before any fact | `ShouldRefuseNullResponseContentGivenPlacementAndAppendNothing` |
| Exact retry, changed intent (place) | `ShouldReturnOriginalResultGivenExactPlacementRetryAndRefuseChangedActor` |
| Replay of valid history | `ShouldRestoreHoldStateGivenValidPlacedHistory` |
| Retry of committed placement after later release | `ShouldReturnOriginalResultGivenExactPlacementRetryAfterRelease` |
| Competing placement | `ShouldRefuseSecondPlacementGivenHoldAlreadyPlaced` |
| Release exact content, attribution | `ShouldReleaseExactPlacedContentGivenLaterAttributedRelease` |
| Release without placement | `ShouldAppendNothingGivenReleaseOfUnplacedHold` |
| Wrong content/position/revision/time, stale revision | `ShouldAppendNothingGivenReleaseNotMatchingPlacedContentOrRevision` |
| Exact retry, changed intent (release) | `ShouldReturnOriginalResultGivenExactReleaseRetryAndRefuseChangedActor` |
| Competing release | `ShouldRefuseCompetingReleaseGivenHoldAlreadyReleased` |
| Release retry after replayed history | `ShouldReturnOriginalResultGivenExactReleaseRetryAfterReplayedHistory` |
| Bounded history | two facts per stream by construction; payload size covered by `ShouldFitTheBoundedEventPayloadGivenMaximumEscapedReasonAndActor` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `ArtifactLegalHoldPlaced` and `ArtifactLegalHoldReleased` carry source, revision, source position, hold id, reason, attributed actor and time, native operation provenance and response refs. `HoldId` repeats the stream id, which is the established wire shape; no change requested. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: authorization, live source/digest check, personal HTTP requirement, and the PUT routes in the catalog.
- Projections: active and historical hold counts feeding disposition blockers.
- Catalog status `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` and the stale `baselineSourcePath` (`.../Retention/ArtifactRetention.cs`) are shared-file items for the catalog owner.

## Evidence

- Jev: not consulted; the decisions follow the catalog and the code directly.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArtifactLegalHold|FullyQualifiedName~ArtifactRetentionTests"`.
- RED: none captured; not claimed.
- Compile risks to watch at build: `DomainEventSeed.Attach(copy, holdId, 2UL)` overload shape, and `Record.Exception` with a statement lambda.

## Review before build

Independent review of source and tests, written, not executed.

### Findings and fixes

- Tests asserted `RequestErrorKind.Conflict` for native-replay conflicts (changed actor on retry, competing second placement). The kind carried by `AcceptedAppendResult.ConflictError` was unconfirmed, so those three assertions now compare against `AcceptedAppendResult.ConflictError` itself (the sibling-test pattern). Fixed.
- Coverage gap: null response refs on `ReleaseHold` had no test. Added `ShouldRefuseNullResponseContentGivenReleaseAndAppendNothing`. Fixed.
- Implementation: no correctness gap found. Logical-id and null guards run before the replay decision; retries return the original result with no event (including a placement retry after release); competing placement and release are refused with no fact; `Apply` re-checks the same invariants for replay.
- Fixture values were checked against the validators: lowercase 64-hex digests, supported source kind `application_import`, tenant matching the source, member actors from `NativeFacts.Root`, reason length at most 2000.

### Jev (model jev-1.14.0, advisory; request/response in implementation-jev/artifactlegalhold-test-impl-review-2026-10-29-*.json)

| Question | Answer | Probability |
|---|---|---|
| HoldId repeats stream identity in both payloads | keep_established_wire_shape | 0.79 (remove 0.05, needs_source_resolution 0.16; confidence 0.69) |
| Place retry after release | ack_original_no_event | 0.97 |
| Release rule enforces the invariant | noul | 0.93 |
| Changed-actor retry conflict is consistent | noul | 0.93 |

Disposition: HoldId payload field kept (decisive lead, but 0.79 with 0.16 on source resolution; no event or schema change requested, flagged for the catalog owner). Other answers support the current code and tests.

### Remaining risks

- Not compiled: `DomainEventSeed.Attach(copy, holdId, 2UL)` overload and generated `ComplianceCoreJsonContext.Default.ArtifactLegalHoldReleased`; `Record.Exception` statement lambdas.
- Assumes `AggregateScenario.PendingEvents` excludes replayed (Given) facts, as sibling tests do.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
