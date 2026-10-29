# TenantManagerGuard aggregate review (2026-10-29)

## Boundary decision

Keep together. `TenantManagerGuard` is the tenant singleton (stream `{tenant}/rbac-manager-guard/{tenant}`, derived from the tenant id only; no caller key forks it). It owns one atomic decision: a withdrawal (suspend, deprovision, administrators-team removal) must leave at least one other available tenant administrator, and administrator admission selects the exact historical membership episode and its pending restoration. Contributors, pending transitions and one selected episode must be decided together, because separating them would let two withdrawals each see the other administrator as available.

Exclusions: `Member` decides member lifecycle and `TeamMember` decides assignment; both are source owners whose facts the guard admits, not duplicates. Process drive, content publication and projections are handlers/reactors. Full chronology is external; the guard retains only available contributors, live pending transitions and the selected episode.

## Lifecycle

| State | Command | Result |
| --- | --- | --- |
| empty | first admission (register) | pins source-cut baseline, accepts restoration |
| available admin | withdrawal that leaves another available admin | `TenantManagerWithdrawalAccepted`, pending |
| available admin | withdrawal of the last available admin | refused (`LastAdministratorConflict`), no fact |
| pending withdrawal | complete | `TenantManagerWithdrawalCompleted`, pending cleared |
| unavailable admin | reinstate or re-register | `TenantManagerRestorationAccepted` then `TenantManagerRestored`/`TenantManagerRestorationCompleted` |
| same command id, same intent | retry | original response replayed, no new fact |
| same command id, changed intent | retry | conflict, no fact |

## Changes

- Source: none. Existing behavior satisfies the criteria; no speculative change made.
- Test added: `ShouldAppendNoGuardFactGivenDeprovisioningLastAvailableAdministrator` (the only withdrawal kind without a last-administrator refusal test; suspension and removal were covered).

## Test to criterion

| Criterion | Tests |
| --- | --- |
| Stable identity | singleton address in constructor; `ShouldPinSourceCutOnFirstFactGivenFirstAdmission` |
| Named invariant, refusal appends no fact | `ShouldRejectWithdrawingLastAdministratorGivenSequentialSuspension`, `ShouldRejectRemovingLastAdministratorGivenAdministratorsTeam`, `ShouldAppendNoGuardFactGivenDeprovisioningLastAvailableAdministrator`, `ShouldRejectDismantlingBuiltInAdministrationGivenRoleOrPermissionRemoval` |
| Competing decisions / stale revision | `ShouldKeepOneActiveAdministratorGivenConcurrentMutualSuspension`, `ShouldAppendNoGuardFactGivenStaleAdministratorAssignment` |
| Exact retry, including after later facts | `ShouldReplayFrozenResponseGivenExactRetryOfEachPublicCommand`, `ShouldReplayOriginalResponseGivenExactRetryAfterLaterLifecycle`, `ShouldReplayFrozenAcceptanceGivenSystemRemovalRetryAfterAssignmentClosed` |
| Changed intent | `ShouldConflictGivenSameCommandIdWithChangedIntent` |
| No-op / neutral | `ShouldAdmitNeutralFactGivenNonAdministratorSuspension`, `ShouldAppendDirectlyWithoutGuardReadGivenNonAdministratorTeamRemoval` |
| Lifecycle | restoration, reinstate, re-registration, old-episode callback, pending fence tests in `TenantManagerGuardTests` |
| Replay of valid history | `TenantManagerNativeHistoryTests` (compacted history, pinned baseline) |
| Bounded history | `TenantManagerNativeBoundaryTests` |
| Lost acknowledgment | `TenantManagerPrepareRecoveryTests` |

## Event contract status

No event changes needed. Facts carry bounded affected-episode authority and operation provenance; the reducer checks tenant, member, episode and emitting owner on replay.

## Later-stage follow-ups (not aggregate gaps)

- Handler/reactor drive of pending transitions to completion and any recovery failures observed in handler or process tests.
- Projection of administrator roster from guard facts.

## Evidence

Tests are written, not executed; build and run pending. No Jev request was made for this slice, and no event names changed. The 2026-10-27 focused run (47 passed with `TenantTests`) predates the added test and is not evidence for it.

## Review before build

Findings
- Test defect, fixed: the added test called `DeprovisionAsync(only, only)`. `DeprovisionMemberHandler` refuses self-deprovision before the guard is consulted, with a different non-guard conflict, so `Assert.Same(LastAdministratorConflict, ...)` would fail and the guard would never be exercised. Rewritten as `ShouldAppendNoGuardFactGivenDeprovisioningLastAvailableAdministrator`: administrator A deprovisions B (accepted, pending), then B attempts to deprovision A; the guard plan refuses with the shared conflict, fact count is unchanged and available count stays 1. This mirrors the existing suspension retaliation test and the admission path (`guard.Plan` runs without an actor-availability check).
- Source: no defect found. Refusal produces no fact, duplicate reasons conflict, stale revision and exact retry are covered by existing tests. Renamed test; names and Arrange/Act/Assert phases follow CNTRYL0001/0002; no new usings or types.

Jev (jev-1.14.0, 2026-10-29; request/response in `implementation-jev/tenantmanagerguard-test-impl-review-2026-10-29-*.json`)
- Does the scenario demonstrate the deprovision invariant: 0.94 true. Applied (test kept).
- Keep pending withdrawals and available contributors under one guard: 0.86 true. Consistent with the Keep-together decision; no change.
- No catalog, event or payload discrepancies found, so no Choice questions were needed.

Remaining risks
- Written, not executed. Assumes retaliation by B (whose deprovision is pending, not driven) is not refused earlier than `guard.Plan`; the admission code shows no such check.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
