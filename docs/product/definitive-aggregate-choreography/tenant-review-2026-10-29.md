# Tenant aggregate review

Reviewed 2026-10-29 against the catalog row `Tenant` (aggregate-details.md, aggregates.json) and decision `TENANT_Lifecycle` (explicit state machine, Jev 0.93; owner 0.89). Scope is the aggregate stage only.

## Identity, invariant, exclusions

- **Identity:** the caller `TenantId` is the immutable tenant creation identity, stream `tenants/<TenantId>`; lifecycle operations bind the caller `CommandId` through `NativeAcceptance`. Observation: `RegisterTenantHandler` still opens `new Tenant(context.RequestId)`, so the stream key is a request key and `RegisterTenant` has no `TenantId` field. That is handler/command-stage work (follow-up), not an aggregate gap.
- **Named invariant:** one tenant lifecycle activates only after its slug and first-administrator conditions; offboarding forbids new active service; one current slug transition is admitted at a time. Lifecycle: registered becomes active only after the slug is confirmed and the first administrator is assigned; active and suspended alternate only by an acting platform operator; active or suspended may start offboarding; offboarding completes only after the attest-record package delivery decision and retention disposition are recorded; every other transition is refused and a repeat of the current state is a no-op.
- **Exclusions:** global slug uniqueness (`TenantSlug`), invitations and mailbox claims, platform operator roster, delivery of the attest-record package and retention disposition (other owners), directory projections.

## Boundary decision

Keep together, narrow: lifecycle, activation and the current-slug link need one atomic decision; slug uniqueness is already split into `TenantSlug`. History is bounded by tenant lifecycle and slug transitions (low contention, no roster). Offboarding completion depends on facts owned elsewhere, so it is a coordinated step (reactor/command stage), not a check the Tenant can make from its own state.

## Lifecycle

| State | Decision | Result |
| --- | --- | --- |
| absent | Register (valid) | `TenantRegistered`; slug pending |
| registered (any, including a surrendered slug) | Register again | conflict, no fact (changed this round for a surrendered slug) |
| slug pending/confirmed, activation not required or done | Suspend | `TenantSuspended(V2)` -> suspended |
| registered, slug pending, rejected, or activation outstanding | Suspend | conflict "not active", no fact |
| suspended | Suspend | legacy: no-op; native: original decision recorded, state unchanged |
| suspended | Reactivate | `TenantReactivated(V2)` -> active |
| active | Reactivate | legacy: no-op; native: original decision recorded, state unchanged |
| active or suspended | Offboard | `TenantOffboardingStarted(V2)` -> offboarding |
| slug pending, activation outstanding | Offboard | conflict, no fact (changed this round) |
| slug rejected | Offboard | not found, no fact |
| offboarding | Offboard | legacy: no-op; native: original decision recorded |
| offboarding | Suspend / Reactivate / Activate / slug surrender request | conflict, no fact |
| offboarding | Complete offboarding | not modelled (see event contract status) |

## Changes this round

- Gap 1 (real): offboarding could start from a tenant still awaiting slug confirmation or activation; the state machine allows it only from active or suspended. `StartOffboarding` and `PrepareLifecycle(OffboardTenant)` now refuse a tenant that never entered service and is not suspended (`HasEnteredService`: slug ever confirmed and activation done or not required). A previously active tenant keeps this status during a slug transition. The rule applies to fresh decisions only: replay of committed lifecycle facts uses the unchanged `CheckLifecycle`, so streams committed earlier remain hydratable.
- Gap 2 (real): `Register` used `_slug is not null` as "already registered", so after `TenantSlugSurrenderConfirmed` cleared the slug a second `TenantRegistered` could be appended to the same stream. A private `_registered` flag, set by the `TenantRegistered` fold, now makes the creation identity single-use.
- No other aggregate behavior changed. Source: `src/Compliance.Core/Features/Tenants/Aggregates/Tenant.cs`.

## Done-criteria status

| Criterion | Status | Evidence |
| --- | --- | --- |
| Stable identity | holds in aggregate; handler keys on request id (follow-up) | TenantId is the stream id |
| Named invariant, owned lifecycle | in place | table above |
| Invalid decisions append no events | covered | `ShouldRefuseOffboardingGivenSlugAwaitingConfirmation`, `...InvitedAdministratorNotYetActivated`, `...RejectedSlugRegistration`, `ShouldRefuseSuspensionGivenOffboarding`, `ShouldRefuseRegistrationGivenSurrenderedSlug`, existing `ShouldRequireReasonGivenTenantLifecycleDecision` |
| No-op | covered | existing `ShouldPreserveHistoryGivenSuspensionAndReactivation`, `ShouldRevokeTenantAccessGivenOffboarding` |
| Exact retry vs changed intent, retry after later facts | covered by existing `TenantLifecycleIdentityTests` (native path through `NativeAcceptance`) | unchanged |
| Replay of valid history | covered | `ShouldRestoreLifecycleStateGivenReplayOfValidHistory` |
| Stale revision | covered | `ShouldRefuseLifecycleDecisionGivenStaleRevision`; changed-revision retry in `TenantLifecycleIdentityTests` |
| Offboarding source states | covered | `ShouldStartOffboardingGivenSuspendedTenant`, `ShouldPermitNativeOffboardingGivenSuspendedTenant`, `ShouldRefuseNativeOffboardingGivenSlugAwaitingConfirmation` |
| Bounded history | lifecycle-bounded; framework owns event-store history | not claimed beyond that |
| Offboarding completion | open | below |

## Event contract status

- Existing lifecycle facts (`TenantRegistered`, `TenantSlug*`, `TenantSuspended(V2)`, `TenantReactivated(V2)`, `TenantOffboardingStarted(V2)`, `TenantActivated(V2)`) are unchanged; V2 facts carry provenance and the original result, and V1 remain read-compatible.
- `TenantOffboardingCompleted` exists in Common with the payload `(TenantId, SubjectId, Revision, ActorMemberId, OccurredAt)`. That is a generic subject-revision shape: it carries neither the operation provenance, the export reference nor the retention decision reference that `CompleteTenantOffboarding` requires, and it is not wired into the aggregate. No completion transition was added, because the preconditions are owned by other aggregates and the fact needs a contract change first. Reported as an event change needed.

## Later-stage follow-ups (not aggregate gaps)

- `RegisterTenantHandler`: open the aggregate by a caller `TenantId` (add the field to `RegisterTenant`) instead of `context.RequestId`.
- `ReactivateTenant`: the catalog requires `ExpectedRevision` (CMD_ConcurrencyGuards); the command and handler pass none and `PrepareLifecycle` currently requires none for reactivation. Change command, handler and aggregate together.
- `CompleteTenantOffboarding`: handler plus a reactor/process that confirms the attest-record package delivery decision and retention disposition before asking the Tenant to complete.
- Catalog says `SuspendTenant`/`OffboardTenant` revision guard rule is unrecorded while code already requires `ExpectedRevision`; reconcile the catalog.

## Evidence

Tests are written, not executed; build and run pending. Nothing was built, formatted or run in this session. The implementer sent no Jev request; the independent review below did. Files changed: `Tenant.cs`, new `TenantLifecycleStateMachineTests.cs`, this review.

## Review before build (2026-10-29)

Independent review of source, tests and this file; nothing built or run.

### Findings and fixes

- Implementation verified: `HasEnteredService` (slug ever confirmed and activation done or not required) gates fresh offboarding in `StartOffboarding` and `PrepareLifecycle`; replay uses `CheckLifecycle` so committed streams hydrate. `_registered` is set only by the `TenantRegistered` fold, so a surrendered slug cannot be re-registered. No duplicate-append path found: refusals return before `RaiseEvent`; repeats of offboarding return success without a fact.
- Existing suites that offboard (`TenantTests`, `TenantLifecycleIdentityTests` seed register plus confirm with no activation requirement) still pass the new guard by reading; the invalid-reason test is refused by validation before the guard.
- Test gap fixed: no test proved an invited administrator who has activated may offboard. Added `ShouldStartOffboardingGivenInvitedAdministratorActivated` (prepare and legacy start both succeed, one `TenantOffboardingStarted`).
- Compile read: `PendingEvents.Count`, `.OfType<>`, `.ToArray()`, `Given(history).Aggregate` and the `TenantFacts` extension overloads all match committed usage; names and Arrange/Act/Assert phases follow CNTRYL0001/0002; fixture GUIDs are valid; `Activate(Uuid, string?)` resolves to the legacy overload. The redundant `using Bdgrz.Compliance.Tests;` matches `TenantTests.cs`. Could not confirm by compiling.
- Observation, not changed: legacy `Suspend`/`Reactivate`/`StartOffboarding` do not reject an empty operator id (the native path does). Legacy facts are read-compatibility only; handlers use the native path.

### Jev (model jev-1.14.0; advisory; request and response in `implementation-jev/tenant-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability | Disposition |
| --- | --- | --- | --- |
| Native repeat suspend: record original decision vs acknowledge without fact | record_original_decision | 0.92 (confidence 0.88) | Decisive: current behavior kept; catalog wording should say a repeat changes no state but the native command still records its original outcome |
| Offboard refusal for invited admin not yet activated follows the rule | true | 0.96 | Confirms guard |
| Test asserts the offboarding source-state rule | true | 0.93 | Confirms test |
| Second Register after surrender follows immutable creation identity | true | 0.86 | Confirms `_registered` |
| Completion fact payload: replace with native fact vs keep generic vs needs source | keep_generic_payload | 0.54 vs 0.32 vs 0.14 (confidence 0.31) | Near-even, unresolved; no change; stays in eventChangesNeeded |

### Remaining risks

- Not built or executed.
- `TenantOffboardingCompleted` contract and the completion transition remain open (Jev unresolved).
- Offboarding source guard is not applied on replay; confirm no committed offboarding from a never-in-service tenant exists before tightening.
- Catalog wording on native no-op repeats and `ReactivateTenant` `ExpectedRevision` needs reconciliation (shared files, not edited).


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
