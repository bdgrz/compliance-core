# UserIdentity aggregate review

Verified 2026-10-29 against the polished catalog (aggregates.md/aggregate-details.md row `UserIdentity`, events `UserIdentityRegistered`, `UserIdentityRevoked`, `UserIdentityAuthenticated`, `UserIdentityProfileObserved`, `RecoveryIdentityProtected`, `RecoveryIdentityProtectionSettled`). Backlog group 70.

## Identity, invariant, exclusions

- **Identity:** UUIDv5 of provider + subject (`CreateIdentityId`), stream `user-identities/<id>`. No caller key forks a stream; hydration by id, by provider+subject, or with an explicitly selected recovery challenge/command all resolve the same stream.
- **Named invariant:** an external provider+subject associates to exactly one platform user; a revoked identity cannot authenticate and has one exact replacement. Recovery protection (slot held for one challenge and account) is decided on the same stream so registration and revocation cannot race a held slot.
- **Exclusions:** email reservation (`EmailAddress`), recovery process orchestration (`IdentityRecovery`), user directory and session projections, presentation names (audit only, never authority), replacement identity existence (handler).

## Boundary decision

Keep together. Registration, revocation and the recovery slot hold are all decided over one provider identity's association; no roster or projection is a participant. Authentication and profile observation are audits on an independent physical stream and do not increment the aggregate history. No split or reactor needed.

## Lifecycle

| State | Command | Result |
| --- | --- | --- |
| absent | Register(valid or generated user) | `UserIdentityRegistered`, success |
| absent | Register(empty user id) | validation refusal, no fact |
| absent, slot protected for another account | Register | recovery conflict, no fact |
| absent | Revoke | not found, no fact |
| registered | Register same user (or none) | success, no fact |
| registered | Register other user | conflict, no fact |
| registered | Authenticate / ObserveAuthenticatedProfile | audit only |
| registered | Revoke(self or empty replacement) | validation refusal, no fact |
| registered | Revoke(replacement) | `UserIdentityRevoked` |
| revoked | Revoke same replacement | success, no fact (exact retry) |
| revoked | Revoke other replacement | conflict, no fact |
| revoked | Authenticate / ObserveAuthenticatedProfile | unauthorized, no fact |
| revoked | Register (any user) | unauthorized, no fact (new) |
| any | Protect / Settle / RegisterRecovered / RevokeRecovered | recovery facts per challenge cut, existing coverage |

## Changes this round

- **Gap:** `Register` on an already registered but revoked identity returned success with an `AuthenticatedUserIdentity`, so the OIDC link path (`LinkOidcProviderIdentityHandler`) could hand out authority of a revoked identity. `Register` now returns Unauthorized for a revoked identity after the other-user conflict check; no fact is appended.
- Other criteria already held in source; only coverage was missing.

## Test to criterion

| Criterion | Test |
| --- | --- |
| Stable identity | ShouldDeriveSameAggregateIdGivenSameProviderIdentity |
| Accepted registration, no metadata/extra state | ShouldRecordOnlyProviderIdentityGivenRegistrationWithEmail, ShouldAllowMissingEmailGivenProviderDoesNotSupplyOne |
| Invalid decision, no fact | ShouldRefuseRegistrationWithoutFactGivenEmptyUserId, ShouldRefuseRevocationWithoutFactGivenUnregisteredIdentity, ShouldRefuseRevocationWithoutFactGivenSelfOrEmptyReplacement, ShouldRefuseRegistrationWithoutFactGivenRevokedIdentity (new) |
| Exact retry / changed intent | ShouldReturnExistingIdentityGivenRepeatedRegistration, ShouldReturnConflictGivenIdentityBelongsToAnotherUser, ShouldAcknowledgeRetryAndConflictGivenRevokedIdentityAndChangedReplacement (new) |
| Revoked cannot authenticate | ShouldRejectAuthenticationGivenRevokedProviderIdentity, ShouldRejectProfileObservationGivenRevokedIdentity |
| Valid replay / hydration | ShouldContinueFromReplayedStateGivenRegisteredAndRevokedHistory (new), ShouldHydrateRevokedIdentityByStableIdGivenAnIssuedSessionReference |
| Retry after later facts | revoke retry after replayed revocation (new replay test) |
| Recovery slot protection and fences | UserIdentityRecoveryProtectionTests |
| Historical claims, replay cuts, profile provenance | IdentityOwnerHistoryTests, UserIdentityObservationTests |
| Competing decisions | framework optimistic concurrency; the loser re-decides through the retry/conflict branches above |

## Event contract status

`UserIdentityRegistered` v1 and `UserIdentityRevoked` v1 carry business facts plus optional recovery provenance (operation, challenge, intent). `UserIdentityRevoked.UserIdentityId` duplicates the stream id and is an existing wire contract validated by the reducer; not changed. No event changes needed.

## Later-stage follow-ups

- Handler: existence/registration of the replacement identity before revoke (aggregate only requires a different non-empty id); `LinkOidcProviderIdentity` should surface the new revoked-identity refusal as an authentication failure.
- Projector: directory reconciliation already rejects re-registration of a revoked entry.

## Evidence

Tests are written, not executed; build and run pending. No Jev call made this round. Source change is one branch in `Register`. No malformed-history tests, per user preference.

## Review before build

Independent review 2026-10-29 of source and `UserIdentityLifecycleTests.cs` (nothing built or run).

### Findings

- Implementation: `Register` revoked branch, `Revoke`/`PrepareRevoke` retry and conflict, validation and not-found refusals all hold the invariant; no refusal appends a fact, an exact revoke retry appends nothing, and a changed replacement conflicts. No gap found.
- Tests: all six assert behavior (result kind, pending events, replayed state) and can fail if the rules regress. One strengthening applied: the replay test now also asserts `Register` on the replayed revoked identity is Unauthorized (was only covered in the non-replayed test).
- Compile review: usings sufficient (`Cntryl.Portia` for `Uuid`/`RequestErrorKind`, `Cntryl.Portia.Testing`, aggregates, events); `.Count` property used on `PendingEvents`; `Assert.Empty`; `!` on `Error`; theory uses a bool, no constant array arguments; the replay `Given(DomainEvent[])` matches existing usage (`RbacTeamRoleFacts`). Not confirmed without compiling: `Version == 2UL` after two attached events (single-event precedent gives 1UL) and that `Id` is `Uuid`.

### Jev (model jev-1.14.0; advisory)

Files: `implementation-jev/useridentity-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
| --- | --- | --- |
| Register on a revoked identity | unauthorized_no_fact | 0.96 |
| Repeated revoke handling | same replacement succeeds, changed conflicts | 1.00 |
| `UserIdentityRevoked.UserIdentityId` repeats stream id: disposition | keep_existing_contract | 0.97 |
| Premise: that field repeats the stream identity | true | 0.93 |
| Tests assert behavior and can fail | true | 0.94 |

Disposition: first two confirm current code. The payload duplicate is a real catalog/event gap but a shared wire contract; Jev says keep for now. Reported under remaining as a possible later shared event change (drop `UserIdentityId` from `UserIdentityRevoked`); no event change applied.

### Remaining risks

- Tests not compiled or run; `Version` assertion unconfirmed.
- Handler follow-ups unchanged (surface revoked refusal from `LinkOidcProviderIdentity`; replacement existence check).


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
