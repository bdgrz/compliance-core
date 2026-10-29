# TenantSlug aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched.

## Boundary decision

**Keep together.** `TenantSlug` is the global slug authority (stream `bdgrz/tenant-slugs/{uuidv5(normalized slug)}`). Identity is the existing UUIDv5 of the normalized slug, so no caller key forks a stream; the selected command id binds the exact claimant tenant and episode through native operation provenance.

Named invariant: one normalized global slug belongs to at most one tenant, and once surrendered it is permanently retired (never available again, even to its former owner).

Exclusions: the tenant's current-slug link, activation and one-pending-change rule belong to `Tenant` (narrow boundary). Previous slug history and the change/confirm/reject/surrender-request conversation are tenant facts and reactor work. `TenantSlugChangeRequested` is a Tenant fact and was excluded from the reservation reclassification. Reserved routes are refused at claim time (as a refusal fact); the lookup-only construction (`allowReserved`) exists so a handler can answer for them.

## Lifecycle

| State | Command | Result |
|---|---|---|
| unclaimed | Register by tenant (valid slug) | `TenantSlugRegistered` |
| unclaimed or reserved | Register of reserved route | `TenantSlugRegistrationRejected` |
| claimed by A | Register by A | no fact (`AlreadyClaimed`) |
| claimed by A | Register by B | `TenantSlugRegistrationRejected` |
| claimed by A | Surrender by A | `TenantSlugSurrendered`, retired |
| claimed by A | Surrender by B | `TenantSlugSurrenderRejected` |
| unclaimed | Surrender by any tenant | `TenantSlugSurrenderRejected`, no owner created |
| retired (owner A) | Surrender by A | no fact (`AlreadySurrendered`) |
| retired (owner A) | Surrender by B | `TenantSlugSurrenderRejected` |
| retired (owner A) | Register by A or B | `TenantSlugRegistrationRejected` |
| any | empty tenant, or operation owned by another stream | validation refusal, no fact |
| any | same logical id, same intent | replay of original acceptance (`NativeAcceptance`) |
| any | same logical id, changed intent | conflict, no fact |

Refusals are retained as native facts so a replayed receiver is never answered twice (EV-D09; the registration and surrender refusals carry Jev 0.61 and 0.58 and stay flagged for review, not changed here).

## Changes

No production change. Qualification found no behavioral defect: identity, invariant, retirement, refusal-as-fact, no-op and native acceptance are already decided inside the aggregate. A direct call of `Register` or `Surrender` with an already accepted logical id is prevented by the handler's acceptance check (`UnitOperations.RunAsync`) and by the reducer's `NativeAcceptance` double-accept guard; the aggregate command does not itself consult acceptance, which matches the other native owners, so no change was made.

Tests added (aggregate only), in `TenantSlugTests.cs`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, normalization | `ShouldDeriveIdentityGivenNormalizedSlug` (existing), `ShouldDeriveDistinctIdentityGivenDifferentSlugs` (new) |
| Reserved route needs lookup construction | `ShouldRefuseConstructionGivenReservedRouteWithoutLookup` (new), `ShouldRejectNewClaimGivenHistoricalSlugLookup` (existing) |
| Competing claims | `ShouldRejectCompetingTenantGivenConcurrentClaim` (existing) |
| Owner-only surrender | `ShouldAllowSurrenderGivenOwningTenant` (existing), `ShouldAnswerRefusalWithoutOwnerGivenSurrenderOfUnclaimedSlug`, `ShouldKeepPriorOwnerGivenOtherTenantSurrendersRetiredSlug` (new) |
| Permanent retirement | `ShouldStayReservedGivenSurrenderedSlug`, `ShouldRejectRegistrationGivenRetiredSlug` (existing) |
| No-op appends nothing | `ShouldReportAlreadyClaimedGivenHoldingTenantPreparesClaim`, `ShouldAppendNothingGivenSurrenderRetriedByAlreadySurrenderedTenant` (new) |
| Invalid decision appends nothing | `ShouldAppendNothingGivenEmptyTenantOnClaimOrSurrender`, `ShouldAppendNothingGivenOperationOwnedByAnotherSlug` (new) |
| Valid replay | `ShouldRestoreRetiredOwnerAndRefuseClaimsGivenValidHistoryReplay` (new) |
| Exact retry after later facts | `ShouldReplayCommittedClaimGivenExactRetryAfterLaterFacts` (new); handler level `ShouldReplayWithoutAppendingGivenAnExactRetryOfAClaim` (existing) |
| Changed intent | `ShouldConflictGivenSameLogicalClaimWithChangedIntent` (new); handler level `ShouldConflictGivenTheSameClaimIdentityForAnotherTenant` (existing) |
| Bounded history | one fact per logical operation, no growing collections in state (owner id and retired flag only) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event changes needed. `TenantSlugRegistered`, `TenantSlugRegistrationRejected`, `TenantSlugSurrendered` and `TenantSlugSurrenderRejected` carry tenant id, slug, command id, operation provenance and first response. The slug repeats the stream identity as a business fact for readers; the reconciled catalog keeps it.

## Later-stage follow-ups (not aggregate gaps)

- Reactor drive of Tenant confirm/reject and surrender answers from these facts (`TenantSlugReactor`).
- Member-only redirect projection from the retired owner.
- Revisit the two low-confidence refusal facts (0.61, 0.58) if Jev review changes the process-reply question.

## Evidence

Tests are written, not executed; build and run pending. No Jev request was made for this slice. Test helpers used (`TenantFacts.Provenance`, `Response`, `Register`, `Surrender`, `DomainEventSeed.Attach`) were read but not compiled.

## Review before build

Independent review on 2026-10-29, before any build. Source, tests and helpers (`TenantFacts`, `DomainEventSeed.Attach`, `AggregateScenario.Given`, `AcceptedOperationBinding.Of`, `OperationProvenance` record `with`) were read as a compiler would.

Findings: no correctness gap and no test that cannot fail. Every decision enforces the invariant (claim on a retired or foreign-held slug is a refusal fact; only the owner's surrender retires; no-ops return `AlreadyClaimed` / `AlreadySurrendered` with no fact; foreign or empty operations append nothing). Fixtures are valid (distinct valid slugs, valid 64-char intent hash, provenance owned by the correct stream). The expression-bodied tests carry no Arrange/Act/Assert comments, as with the existing one; CNTRYL0002 applies to block bodies. No fixes were needed.

Jev (model jev-1.14.0, advisory), saved in `implementation-jev/tenantslug-test-impl-review-2026-10-29-request.json` and `-response.json`:

| Question | Answer |
|---|---|
| Retired-claim replay test asserts permanent retirement | noul 0.96 |
| Non-owner surrender of an unclaimed slug leaves it available | noul 0.97 |
| Slug repeated in payloads | keep_for_now 0.96 (confidence 0.94); catalog unchanged |
| Refusal facts (`TenantSlugRegistrationRejected`, `TenantSlugSurrenderRejected`) | keep_as_event 0.93 (confidence 0.89); earlier 0.61 / 0.58 flags are now clear on this question |

Discrepancies: none between code and catalog; both dispositions above are decisive and need no catalog edit.

Remaining risks: tests not executed. Unconfirmed at compile time only: the `Assert.Throws` expression-bodied test, and whether the analyzers accept expression-bodied tests without phase comments (the pre-existing identity test sets the precedent).


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
