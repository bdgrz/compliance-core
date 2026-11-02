# FirmProfessionalDutyDesignation aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and the change process are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (selected split from `FirmProfessionalDutyCatalog`, already applied).** One owner per (canonical user, duty, explicit scope) decides atomically the one active signed designation, the monotonic signed authority revision and the continuity proof of the latest designation content.

- Identity: `IdentityOf(userId, duty, tenantId)`; the platform scope is the literal `platform`, a tenant scope is `tenant:{id}`, so an empty tenant UUID can never alias the platform stream. The selected logical/root ids only choose the hydration cut; they do not enter the stream key.
- Invariant: one pending change at a time, applied at the exact predecessor revision; revision advances by one per applied change; no change is admitted while an engagement use holds the designation.
- Exclusions: signature and staff-proof verification, assertion claim uniqueness (`ProfessionalAssertionIdentityClaim`), staff state (`FirmStaffRecord`), directory projection, scope support check (`ProfessionalDutyRules.IsSupportedDutyScope`, enforced at admission before the owner is opened).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none/applied, nothing pending, no active use | AcceptChange (own owner, logical = root, expected = revision) | pending | `ProfessionalDutyChangeAccepted` |
| any | AcceptChange, stale revision | conflict | none |
| any | AcceptChange, foreign owner or logical != root | validation | none |
| pending | AcceptChange, same assertion | conflict (permanent) | none |
| pending | AcceptChange, other assertion | conflict (transient) | none |
| active engagement use | AcceptChange / Apply | transient conflict | none |
| pending | Apply (exact root) | applied | `FirmProfessionalDutyDesignationRecorded` or `...Revoked`, revision + 1 |
| pending | Reject (exact root) | rejected | `ProfessionalDutyChangeRejected` |
| none pending / other root | Apply, Reject | conflict | none |
| recorded, nothing pending | AuthorizeEngagementUse / SettleEngagementUse | use held / settled, revision unchanged | `...EngagementUseAuthorized` / `...EngagementUseSettled` |
| none recorded | AuthorizeEngagementUse | conflict | none |

Exact retry of an applied change is answered from the selected terminal receipt (`TerminalFor`, `AcceptedFor`) even after later refreshes; the process tests cover it.

## Changes

None to the aggregate. Qualification found no real gap: identity is closed over subject/duty/scope, every refusal path returns before `RaiseEvent`, the replay cut guards every append, payloads carry business facts (provenance is the native-operation envelope required by the catalog), and history is bounded by the selected-root hydration. Only the missing direct aggregate test file was added.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, platform vs tenant vs empty tenant | `FirmProfessionalDutyDesignationTests.ShouldKeepIdentityStableAndDistinctGivenSubjectDutyAndScope` |
| Valid acceptance, payload | `ShouldAcceptOnePendingChangeAtRevisionZeroGivenFreshDesignation` |
| Competing change refused | `ShouldRefuseSecondChangeWithoutFactGivenAnotherChangeStillPending` |
| Same-assertion retry/changed intent refused | `ShouldRefuseSameAssertionWithoutFactGivenChangeAlreadyAccepted` |
| Stale revision | `ShouldRefuseChangeWithoutFactGivenStaleExpectedRevision` |
| Invalid decisions append nothing | foreign-owner/logical-id, no-pending Apply/Reject, other-root Apply/Reject, engagement use without designation tests |
| Exact retry after later facts, replay of valid history, rejection fence, staff proof, frozen decision instant, use hold/settle, bounded history | existing `ProfessionalDutyChangeTests`, `ProfessionalDutyNativeHistoryTests`, `EngagementPersonalDecisionTests`, `ProfessionalDutyGovernanceCompositionTests` |

Malformed-history tests were not added, by user preference.

## Event contract status

Six events (`ProfessionalDutyChangeAccepted`, `ProfessionalDutyChangeRejected`, `FirmProfessionalDutyDesignationRecorded`, `...Revoked`, `...EngagementUseAuthorized`, `...EngagementUseSettled`) match the catalog list. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Catalog backlog row says "No direct story mapping"; selected stages (to `FirmStaffRecord`, `ProfessionalAssertionIdentityClaim`) belong to the process/reactor stage.
- Handler receipts and HTTP routes remain later-stage work.

## Evidence

- Jev: no request this round; no open design decision.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~FirmProfessionalDutyDesignationTests"`, then the unit suite.
- RED: none captured; no gap was found, so none is claimed.
- Residual risk: the new file was not compiled; `Stream.Area` and `ActorReference.ForPlatformOperator` usage are by reading.

## Review before build (independent reviewer, 2026-10-29)

Nothing was built or run. Source and tests were read line by line as the compiler and analyzers would.

### Findings

- Implementation: no gap. Every refusal in `AcceptChange`, `Apply`, `Reject`, `AuthorizeEngagementUse` returns before `RaiseEvent`; stale, duplicate, competing and foreign-owner inputs append nothing; the replay cut guards every append. No change made.
- Tests: assertions are behavioral (events appended or not, pending, revision). Fixtures checked against their types (`ImmutableContentRef` is an unvalidated positional record, 64-char sha; `OperationProvenance` has 12 required positional arguments, matched; `Owner` and `LogicalId` overrides via `with`). Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. No change needed.
- Limits kept honest: the tests only exercise a fresh, un-replayed owner. After an in-process accept the replay cut is unverified, so a positive `Apply`/`Reject`/engagement-use append, and replay of recorded/revoked history, are covered only by the existing process and history tests (not re-proved here).
- Positive observation about the retry rule: the owner refuses the same assertion with a permanent conflict while pending; exact retries are answered by the process from `AcceptedFor`/`TerminalFor`.

### Jev (jev-1.14.0; advisory), files `implementation-jev/firmprofessionaldutydesignation-test-impl-review-2026-10-29-*.json`

| Question | Answer | Probability | Disposition |
|---|---|---|---|
| Do t2-t6 assert the one-pending-at-exact-revision invariant with no fact on refusal? (Noul) | yes | 0.82 | Accepted as a review signal |
| Same-assertion refusal in aggregate consistent with no-new-fact retry? (Choice) | consistent_no_new_fact | 0.99 | Applied: no change |
| Payload `Owner` equals stream address: discrepancy? (Choice) | keep_as_native_operation_envelope | 0.97 (remove 0.02) | Decisive: kept; shared native-operation contract, no event change |
| Exclusions decided outside the aggregate (Noul) | yes | 0.49 | Near-even: UNRESOLVED; exclusions stay as source-stated scope in this file, no code depends on it |

### Remaining risks

- New test file is uncompiled: `Stream.Area`, `AggregateScenario<T>` over a pre-built owner (same pattern as `RbacTeamRoleFacts`), and the `ForPlatformOperator` call were checked by reading only.
- Catalog decision about Advisor/Attest per engagement assignment and independence walls is not enforced in this aggregate; it belongs to the process/staff-stage owners.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
