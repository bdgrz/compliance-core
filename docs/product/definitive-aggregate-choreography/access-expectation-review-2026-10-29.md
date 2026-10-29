# AccessExpectation aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together; retained as its own owner** (catalog `boundaryDecision: selected_split`, group 24; split from `AccessReviewSystemLedger`). One expectation line of one system instance decides, atomically, predecessor/successor competition, independent approval and same-line exceptions at one line revision. Decision `OWNER_AccessExpectationScopeGuard` (Jev 0.99, `line_authority_explicit_scope_override`) limits the exclusion to the same lineage: different lines, including different policies on one system, coexist, and no cross-line governor or ban is implied.

- Identity: tenant realm plus root expectation-line id (`EventStreamAddress(tenant, "access-expectations", lineId)`). The first proposal roots the line, so a new line requires the proposal's logical id to equal the line id; every successor names the exact approved predecessor line and shares its stream. The system instance is bound by the first fact and immutable. No request key can fork a stream.
- Invariant: one exact system-instance expectation lineage owns predecessor/successor competition, independent approval and same-line exceptions, under a monotonically increasing line revision.
- Exclusions: the rule kind, parameter validation, independent proposer/reviewer or exact waiver, and future-expiry exception checks are decided by the caller from `AccessExpectationLine` over verified immutable content and re-checked here only against the line revision (content is a reference, not aggregate state). Rule-kind validation and approver existence are handler and rule concerns. Directories, lists and population analysis are projections.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| empty line | Propose, logical id = line id, expected revision 0 | revision 1, system instance bound | `AccessExpectationProposed` |
| empty line | Propose, logical id not the line id, foreign owner, or no expected revision | validation refusal, no event | none |
| empty line | Propose, stale expected revision | conflict, no event | none |
| empty line | Approve or RecordException | not found, no event | none |
| line with facts | Propose, new expectation id, same instance, expected = revision | revision + 1 | `AccessExpectationProposed` |
| line with facts | Propose, existing expectation id or other system instance | conflict, no event | none |
| proposed | Approve, expected = revision | revision + 1 | `AccessExpectationApproved` |
| proposed | Approve unknown expectation | not found | none |
| proposed | RecordException | not found (approved expectation required) | none |
| approved | Approve again | conflict | none |
| approved | RecordException, expected = revision | revision + 1 | `AccessExpectationExceptionRecorded` |
| any | any command, stale expected revision | conflict naming current revision | none |
| replayed line, cut not finalized | any append | transient conflict | none |

Statuses are proposed and approved; exceptions do not change an expectation's status.

## Changes

No production change. Adversarial qualification found no behavioral defect.

- Identity cannot fork: stream is derived from tenant and line id; a new line must be rooted in its first proposal's logical id.
- Exact retry of a committed proposal, approval or exception after later facts is decided by `NativeAcceptance` over the fact carrying the selected logical id, never by current line content. Changed intent under a used logical id is decided at the native boundary; each append additionally requires the exact finalized hydrated cut (`AccessReviewReplayCut.Require`).
- Competing successors of one accepted predecessor serialize on the single line revision (second sees a stale revision).
- Invalid decisions raise nothing: every `Prepare*` guard runs before `RaiseEvent`.
- Event payloads carry tenant, system instance, revision, business fields, verified content references and the native operation provenance; no metadata is duplicated.
- History is bounded: the aggregate retains scalars and the selected proposal/approval only, no collection (existing `ShouldKeepBoundedExpectationAuthorityGivenManyApprovedSuccessorsOnOneLine`).

Coverage gap closed (tests only, new file `AccessExpectationAggregateTests.cs`): the existing tests exercised the line only through handlers and the fixture. There was no owner-level coverage of the guards, refusals that append nothing, selected-fact retry after later facts, or valid replay.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, revision 1, instance bound, single event | `ShouldRaiseRevisionOneProposalGivenNewLineRootedInItsFirstProposal` |
| Identity cannot fork (root id, foreign owner, no expected revision), nothing appended | `ShouldAppendNothingGivenProposalNotRootedInLineIdentityOrWithoutOwnRevision` |
| Stale revision appends nothing | `ShouldAppendNothingGivenStaleExpectedRevisionOnNewLine` |
| Refusal with no fact (approve/exception on empty line) | `ShouldAppendNothingGivenApprovalOrExceptionOnLineWithoutFacts` |
| Replay of valid history | `ShouldRestoreLineStateGivenValidProposedApprovedAndExceptionHistory`, `ShouldSelectNothingGivenHistoryHydratedForAnotherExpectation` |
| Exact retry of committed fact after later facts; unaccepted operation | `ShouldFindCommittedApprovalGivenSelectedOperationAfterLaterException` (handler level: `ShouldReturnOriginalViewGivenExactApprovalRetryAfterLaterException`, `ShouldReturnOriginalViewGivenExactProposalRetryAfterLaterLineChange`) |
| Changed intent refusal | `ShouldReturnOriginalExceptionGivenExactRetryAndConflictGivenChangedIntent` (existing) |
| Approval guards, no-op/duplicate approval, stale | `ShouldPrepareApprovalOnlyGivenProposedExpectationAtCurrentRevision`, `ShouldRefuseSecondApprovalAndPrepareExceptionGivenApprovedExpectation` |
| Exception requires approved expectation | `ShouldRefuseExceptionGivenProposedButUnapprovedExpectation` |
| Proposal guards (duplicate, instance immutability, stale) | `ShouldPrepareProposalOnlyGivenNewExpectationOnSameSystemInstanceAtCurrentRevision` |
| Append requires finalized cut | `ShouldAppendNothingGivenApprovalOnReplayedLineWithoutFinalizedCut`, `ShouldRejectUnselectedIntermediateMetadataSubstitutionGivenActualHydratedLineCut` (existing) |
| Competing successors on one predecessor | `ShouldConflictGivenTwoSuccessorsRacingOneAcceptedPredecessor` (existing) |
| Independent lines do not contend | `ShouldKeepUnrelatedExpectationLinesIndependentGivenSeparateRoots` (existing) |
| Same scoped id on independent first owners | `AccessExpectationIdentityTests` (existing) |
| Predecessor/waiver/instance/expiry rules | `ShouldRejectSuccessorGivenUnapprovedOrUnknownPredecessor`, `ShouldRejectApprovalGivenProposerWithUnknownWaiverOrWrongInstance`, `ShouldRejectExceptionGivenUnapprovedSuccessorWrongInstanceOrPastExpiry` (existing, handler/rule level) |
| Selected history and read model | `AccessExpectationHistoryTests` (existing) |
| Bounded history | `AccessExpectationNativeBoundaryTests` (existing) |
| Replay after unavailable content | `AccessExpectationPreparationReplayTests` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

- `AccessExpectationProposed` (`bdgrz.access_expectation.proposed`), `AccessExpectationApproved` (`bdgrz.access_expectation.approved`) and `AccessExpectationExceptionRecorded` (`bdgrz.access_expectation.exception_recorded`), all declared at discriminator version 2 in code while the catalog lists 1, are the catalog's three events; `preliminaryNewEvents` is empty.
- No add, rename, remove or payload change is requested. Note for the catalog owner: all three events are declared at discriminator version 2 in code while `aggregates.json` records version 1; this needs reconciling in the shared catalog, not in the aggregate.

## Later-stage follow-ups (not aggregate gaps)

- Handler: confirm each command selects its `CommandId` as logical id (propose of a new line selects the line id), and keep rule-kind, waiver and approver-existence checks at the handler/rule layer.
- Projectors: expectation directory, line views and population analysis are projections of the line stream.
- Cross-line ban: remains a separately reviewed product decision with a precise predicate; none is implied.

## Evidence

- Jev: no new request this round. Retained adjudication `OWNER_AccessExpectationScopeGuard` (0.99 `line_authority_explicit_scope_override`, decisions.json).
- Tests: written, not executed; build and run pending. Intended command: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessExpectation"`, then the backend unit suite.
- RED: none captured. The new tests cover behavior already implemented, so no failing run is expected; this is not a claimed red.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review, 2026-10-29. Tests were not built or run.

Findings:
- No implementation defect. Every append goes through `Prepare*` guards, then `AccessReviewReplayCut.Require` (finalized exact cut, selected logical id equals the operation, no already-accepted fact), then `RaiseEvent`; stale revision, duplicate approval, instance change and unapproved exception append nothing. Payloads carry no metadata duplicates.
- Tests: each asserts appended/not-appended events or replayed state; none is structural. Names and Arrange/Act/Assert phases comply with CNTRYL0001/0002. Fixture values are valid (Uuid v4 ids, `NativeFacts.Ref()` content refs, root provenance on the owner's own stream, history built with `NativeFacts.Raised` for the proposal and `DomainEventSeed.Attach` only on freshly constructed events).
- Documentation inaccuracy fixed: the implementer recorded only `AccessExpectationProposed` as version 2; all three events are version 2 in code against catalog version 1.
- Coverage note: no owner-level test appends a successor proposal or approval on a finalized cut, because the cut is confirmed by the replay host (internal digest). Append-on-valid-cut is covered by the existing handler/fixture tests.

Compile review (read as compiler): `PendingEvents` used via `Assert.Single`/`Assert.Empty` (no `.Count()`); `Latest` is a `DomainEvent?` accepted by `Assert.IsType`; stale maps to `RequestErrorKind.Conflict` (confirmed in `VersionConflictRequestErrors`); `ActorReference.ForMember(Uuid, string)` and usings confirmed. Unconfirmed: `DomainEventSeed.Attach` generic return type inside the `List<DomainEvent>` initializer (same pattern as other tests), and a possible IDE suggestion on the collection initializer.

Jev (model jev-1.14.0; request/response in `implementation-jev/accessexpectation-test-impl-review-2026-10-29-*.json`):
| Question | Answer | Probability |
|---|---|---|
| Second-approval/stale-exception test proves duplicate and stale refusal | true | 0.90 |
| Unfinalized-cut test proves nothing is appended | true | 0.87 |
| Catalog discrepancy (version 1 vs 2 only) | no_discrepancy | 0.95 (confidence 0.93) |

Disposition: the version difference is shared catalog bookkeeping, no event change; reported under remaining for the catalog owner.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
