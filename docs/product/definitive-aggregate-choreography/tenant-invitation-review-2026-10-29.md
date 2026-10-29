# TenantInvitation aggregate review (2026-10-29)

## Boundary decision

Keep together. `TenantInvitation` is one immutable invitation offer, identified by the tenant-scoped client-chosen invitation id (stream `{tenant}/tenant-invitations/{invitationId}`; no other caller key forks it). The acceptance logical operation id is derived from it (`TenantInvitationIds.AcceptanceOf`), so both native operations land on the same stream.

Named invariant: an invitation lives 7 days and is accepted at most once, only by the invited verified email address holding the issued token; affiliation is `client_personnel`, `firm_staff` or `guest`, a guest names at least one distinct program with a future end date, and the first administrator must be client personnel. Offer terms and the accepting user must be decided together against one stream, so terms, token hash, expiry and the single acceptance stay in one owner.

Exclusions: `TenantInvitationMailboxClaim` owns at-most-one-pending-per-mailbox (generation is carried here as a fact, never decided here). Delivery attempts and the effect payload belong to `TenantInvitationDeliveryProcess`. Token derivation, mailbox acquisition, trusted-issuer sign-in and member creation after acceptance are handlers/reactors.

## Lifecycle

| State | Command | Result |
| --- | --- | --- |
| absent | invite, valid terms, expiry = now + 7 days, claim generation >= 1 | `TenantMemberInvited` |
| absent | invalid terms, foreign operation id, bad token hash, wrong lifetime, missing claim or guest scope | refused, no fact |
| pending | invite again | conflict, no fact (exact retry is resolved by `Acceptance.Decide` before the command) |
| pending | accept with matching email, token, unexpired, after issue, same guest scope | `TenantInvitationAccepted` |
| pending | wrong email/token, expired, guest ended, before issue | validation refusal, no fact |
| pending | guest scope differs from offer | conflict, no fact |
| accepted | accept by any other user | forbidden, no fact |
| accepted | exact retry of either operation | `Replayed` from `Acceptance`, no new fact |
| accepted/pending | same logical id, changed intent | `Conflict` from `Acceptance` |

## Changes

- Source: `PrepareInvite` now requires `expiresAt == now + Lifetime`. Before, the aggregate accepted any future expiry, so the 7-day rule lived only in the admission service. The reducer is unchanged so existing history with any stored expiry still replays.
- Tests added to `TenantInvitationTests`: the six listed below.

## Test to criterion

| Criterion | Tests |
| --- | --- |
| Stable identity | constructor address; `ShouldAcceptUnderAcceptanceIdGivenInvitationCreatedUnderInvitationId` |
| Named invariant (7-day lifetime) | `ShouldRejectCreationGivenExpiryOtherThanSevenDayLifetime` (new), `ShouldReportExpiredGivenTokenLifetimeOrGuestEndDateHasPassed` |
| Offer rules, refusal appends no fact | `ShouldRejectAdministratorGrantGivenFirmStaffInvitation`, `ShouldRejectMalformedTokenHashGivenInvitationIssue`, `ShouldRejectGuestInvitationGivenMissingOrInvalidScopeAndExpiry`, `ShouldRejectRoleGivenUnsupportedValueOrFirmStaff`, `ShouldRejectCreationGivenForeignOperationOrMissingMailboxClaimOrScope` (new) |
| Acceptance refusals | `ShouldAcceptOnceGivenCurrentUnexpiredToken`, `ShouldRejectAcceptanceGivenWrongRecipientOrUnknownInvitation`, `ShouldRejectAcceptanceGivenGuestMembershipEndDateHasPassed`, `ShouldRejectAcceptanceGivenTimeBeforeInvitationWasIssued` (new), `ShouldRejectAcceptanceGivenGuestScopeDiffersFromOffer` (new) |
| Competing decisions | `ShouldRejectSecondAcceptanceGivenInvitationAlreadyAcceptedByAnotherUser` (new), `ShouldRejectSecondCreationGivenInvitationAlreadyExists` |
| Exact retry after later facts / changed intent | `ShouldReplayOriginalDecisionGivenExactRetryAfterLaterAcceptanceAndConflictGivenChangedIntent` (new), `ShouldSelectNoDecisionGivenUnacceptedOperationOnValidHistory` (new) |
| Valid replay | same new replay tests; `ShouldCarryGuestScopeManifestAndExpiryGivenScopedGuestInvitation`; `TenantMemberInvitedSerializationTests` |
| Lifecycle | role, guest and acceptance tests above |
| Bounded history | at most two facts per stream by construction |

## Event contract status

No event changes needed. `TenantMemberInvited` and `TenantInvitationAccepted` carry business facts plus operation provenance; the reducer checks tenant, invitation id, emitting owner, email and terms on replay. Note for the coordinator: `TenantInvitationAccepted` repeats offer terms (email, affiliation, role, guest scope, inviter) that are derivable from the invited fact; left as is because consumers read it standalone, and removing it would be a wire change outside this stage.

## Later-stage follow-ups (not aggregate gaps)

- Handler/reactor: mailbox claim release after acceptance or expiry; member creation after acceptance; delivery process attempts.
- The admission and accept handlers already call `PrepareInvite`/`PrepareAccept`; confirm handler tests still pass with the lifetime check (they compute expiry from `Lifetime`).

## Evidence

Tests are written, not executed; build and run pending. No red run was executed; the lifetime test is expected to fail against the prior source and pass with the change. No Jev request was made for this slice, and no event names changed.

## Review before build

Independent review of source, tests and helpers (`test/Compliance.Tests/TenantInvitationFacts.cs`) before any build. Nothing was compiled or run.

### Findings

- Implementation: `PrepareInvite` now enforces `expiresAt == now + Lifetime`. The admission service computes `now + TenantInvitation.Lifetime`, and the `TenantInvitationHost` and `TenantInvitationFacts.Invite` helpers use `AddDays(7)`, so existing callers still pass. No other caller supplies a different expiry (`ResponsibilitySetTests` uses its own type).
- Acceptance paths checked: a competing second acceptance is refused as forbidden with no fact; exact retries are resolved by `Acceptance.Decide` before the command; expiry, issue-time and guest-end checks all precede the token comparison and refuse without a fact. No duplicate or wrong fact can be appended by a stale or competing input.
- Tests: every new test asserts an outcome (error kind, no pending events or a single accepted fact, replay decisions), and each can fail against the prior source. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Fixture values are valid: the 64-character digest `'6'*64` and `'c'*64`, the schema from `TenantInvitationAdmission.GuestScopeSchema`, and operation ids built with `Provenance`.
- Compile: `AcceptedOperationBinding.Of`, `NativeAcceptanceDecision.{None,Replayed,Conflict}`, `NativeAcceptance.Decide` and the `OperationProvenance` record `with { AuthoredIntentSha256 }` were confirmed in source. Helper parameters (`expiresAt`, `generation`, `scope`) exist on the shared helpers. `Decide` throws if the candidate logical id differs from the selected one; both replay tests pass the matching id.
- No source or test changes were needed in this review.

### Jev (model jev-1.14.0, advisory; request and response in `implementation-jev/tenantinvitation-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability | Disposition |
| --- | --- | --- | --- |
| Do the 30-day and 1-day refusals assert "lives 7 days"? | yes | noul 0.95 | confirmed |
| Is refusing a second acceptance by another user with no fact consistent with accepted at most once? | yes | noul 0.95 | confirmed |
| `TenantInvitationAccepted` repeats derivable offer terms: keep, remove or needs source? | keep_for_now | 0.83 (remove_now 0.10, needs_source 0.07; confidence 0.74) | decisive; kept, recorded as a wire-change follow-up for the coordinator |
| Does the aggregate satisfy the trusted-issuer and verified-email clause by matching the supplied email, with issuer trust verified upstream? | yes | noul 0.88 | confirmed; the handler stage must verify issuer and email verification |

Discrepancies: the catalog row names `AcceptTenantInvitation` as carrying the creation `InvitationId`, while creation is the `InviteOrganizationMember` command in code; this is catalog wording, not changed here. The `TenantInvitationMailboxClaim` row cites `AcceptanceOf(InvitationId)` as its identity, which looks like a catalog text error for the mailbox claim owner; reported for the coordinator, not edited. No event names or payloads changed.

### Remaining risks

- Tests are still unexecuted; the build must confirm the compile checks above.
- The two replay tests attach the same event instances to two hydrations; both aggregates share the same id, so this should be safe, but it was not run.


## Coordinator build and test evidence (2026-10-29, wave 1)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors (missing `Models` using, `PendingEvents.Count` property, array return type, fresh event copies for replay).
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7021 passed, 5 failed of 7026. The five failures are outside this wave: two `AggregateCommandCoverageTests` (coverage map lacks `ArtifactDownloadGrant`, committed earlier) and three `WeeklyScheduleOriginalResultTests` (week-window scheduling).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed. They cover existing behavior unless the review above names a code change.
