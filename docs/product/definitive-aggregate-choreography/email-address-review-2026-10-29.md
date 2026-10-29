# EmailAddress aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Catalog entry verified: `aggregates.json` owner `EmailAddress` (retained baseline, polished 2026-10-29, `boundaryDecision: narrow`), backlog group 70, status "Source present; verify". Source: `src/Compliance.Core/Features/UserIdentities/Aggregates/EmailAddress.cs`.

## Boundary decision

**Keep together, narrow.** One stream per normalized address: `bdgrz / email-addresses / AddressId`, where `AddressId` is the UUIDv5 of the normalized address (`CreateId`). The reducer's `Learn` rejects any fact whose address is not the normalized form that derives the stream id. No caller key forks the stream; `ChallengeId` (verification, recovery) and `CommandId` (reservation) are operation identities bound to this address, never stream identity.

Named invariant: a normalized address reserves exactly one platform user; verification requires its current challenge; a claimed recovery binds one old/replacement pair.

The aggregate keeps current authority only: owner, verified flag, current verification challenge (hash, expiry), current recovery challenge slot with its single claim, plus the one explicitly selected challenge for retry resolution. Historical claims and completions are composed from history (`EmailAddressHistory`), so state is bounded.

Exclusions: delivery outcomes (`EmailChallengeDeliveryLedger`), recovery lifecycle (`IdentityRecovery`), token key derivation, authorization and rate shaping belong to other owners, handlers or reactors.

## Lifecycle

| State | Command | Result |
|---|---|---|
| empty | ReserveEmail | `EmailAddressReserved` |
| reserved, same user | ReserveEmail | success, no fact |
| reserved, other user | ReserveEmail | conflict, no fact |
| stale expected revision | ReserveEmail | conflict, no fact |
| owned, unverified | IssueEmailChallenge | `EmailChallengeIssued` (replaces the current challenge) |
| verified | IssueEmailChallenge | conflict, no fact |
| owned, unverified, current unexpired challenge, matching token | CompleteEmailChallenge | `EmailAddressVerified` |
| verified | CompleteEmailChallenge | conflict, no fact (new) |
| verified, owned, no outstanding recovery | StartIdentityRecovery | `IdentityRecoveryChallengeIssued` |
| unverified or outstanding recovery | StartIdentityRecovery | conflict, no fact |
| current recovery challenge, valid token, unclaimed | CompleteIdentityRecovery (claim) | `IdentityRecoveryChallengeClaimed` |
| claimed | claim again or retarget | conflict, no fact |
| claimed, exact pair, not completed | CompleteEmailRecoveryChallenge | `IdentityRecoveryChallengeCompleted` (only that challenge marked) |
| reused challenge id (verification, recovery current slot, selected claim) | any issue | conflict, no fact |
| same logical id, same intent / changed intent | any | resolved by native acceptance before the owner |

## Qualification against done criteria

Identity, invariant, lifecycle, exact retry via native acceptance and bounded state were already in place. Two real gaps were found in the owner (both unreachable through current handlers, which resolve retries via native acceptance first, but wrong at the owner level):

1. `PrepareIssueRecoveryChallenge` did not refuse the id of the current recovery challenge when it was unclaimed and expired (or delivery-failed), so the same challenge id could be re-issued with a new token and expiry, changing the intent of an accepted id. The verification path already refused this.
2. `PrepareCompleteChallenge` and the `EmailAddressVerified` reducer accepted a second verification of an already verified address, which would append a second `EmailAddressVerified`.

## Changes

`EmailAddress.cs`:
- `PrepareIssueRecoveryChallenge` also refuses `challengeId == _recoveryChallengeId` with Conflict.
- `PrepareCompleteChallenge` refuses an already verified address with Conflict before any other check.
- `Apply(EmailAddressVerified)` rejects a second verification in replay.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Reserve once, same-owner retry no fact | `ShouldReserveOnceGivenSameOwnerRetry` (existing) |
| Competing owner refused, no fact | `ShouldRejectTakeoverGivenAnotherUserOwnsAddress` (existing) |
| Stable identity from normalized address | `ShouldUseDistinctIdsGivenDistinctAddresses` (existing) |
| Stale revision refused, current admitted, no fact from preparation | `ShouldRejectStaleRevisionGivenReservationAgainstChangedAddress` (new) |
| Verification needs owner, current challenge, token, unexpired | `ShouldAllowCompletionGivenOwnerAndUnexpiredChallenge` (existing) |
| Reissue invalidates old token | `ShouldInvalidateOldTokenGivenReissuedChallenge` (existing) |
| Delivery state is not a fact of this stream | `ShouldReportLedgerDeliveryStatusOfCurrentChallengeOnlyGivenReissue` (existing) |
| Verification challenge id reuse refused | `ShouldRejectReuseOfChallengeIdentityGivenIssuedChallenge` (existing) |
| Second verification appends nothing | `ShouldRejectSecondVerificationGivenVerifiedAddress` (new) |
| Recovery requires verified owned address | `ShouldRequireVerifiedOwnedEmailGivenRecoveryChallenge` (existing) |
| Recovery claim binds one pair, no retarget | `ShouldBindRecoveryChallengeToOneReplacementGivenValidEmailProof` (existing) |
| Recovery token proof validation | `ShouldRejectMalformedRecoveryTokenGivenProofValidation` (existing) |
| Recovery challenge id reuse after expiry refused | `ShouldRejectReuseOfRecoveryChallengeIdentityGivenExpiredRecoveryChallenge` (new) |
| Replay of valid history | `ShouldRestoreOwnerVerificationAndChallengeGivenReplayedHistory` (new) |

Exact retry of a committed fact after later facts and changed-intent refusal are resolved by `NativeAcceptance` and `EmailAddressHistory` (selected-fact comparison) and covered by the native-operation and handler suites, not by this owner's tests. Malformed-history tests were not added, by user preference.

## Event contract status

No event add, rename or removal is needed. The six aggregate events (`EmailAddressReserved`, `EmailChallengeIssued`, `EmailAddressVerified`, `IdentityRecoveryChallengeIssued`, `IdentityRecoveryChallengeClaimed`, `IdentityRecoveryChallengeCompleted`) match the catalog selected events. Delivery events (`EmailChallengeDeliveryFailed`, `EmailChallengeDeliverySent`) in `baselineEvents` are owned by the delivery ledger, not this aggregate.

Catalog note for the catalog owner, not changed here: `implementationStatus` still reads `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED` and `baselineSourcePath` points at `Features/UserIdentities/EmailAddress.cs`; the current path is `Features/UserIdentities/Aggregates/EmailAddress.cs`.

## Later-stage follow-ups (not aggregate gaps)

- Handlers keep resolving retries through native acceptance; `StartIdentityRecoveryHandler` treats the outstanding-challenge conflict as a success to avoid address enumeration.
- Delivery reactors (`EmailChallengeDeliveryReactor`, `RecoveryChallengeDeliveryReactor`) and `IdentityRecoveryEmailCompletionReactor` stay as they are.
- Catalog status and path reconciliation above.

## Evidence

Tests are written, not executed; build and run pending. No RED run observed. The production change is three small guards. Helpers used (`EmailAddressFacts` extensions, `NativeFacts.Raised/Replay`, `AggregateScenario`) were read from existing tests but not compiled. The replay test assumes `NativeFacts.Replay` on a fresh `EmailAddress(string)` with an empty selected operation is accepted, as other aggregates' replay tests do. No Jev request was made for this slice.

## Review before build

Independent review of the source, tests and helpers (`test/Compliance.Tests/Testing/EmailAddressFacts.cs`, `NativeFacts`); nothing compiled or run.

### Findings

- Implementation: both guards confirmed against the code. Without the `_recoveryChallengeId` check, `ClaimOf` returned a null claim for an unclaimed current recovery id, so an expired id could be re-issued with new intent. Without the `_isVerified` check a matching current challenge and token could append a second `EmailAddressVerified`. No payload field duplicates stream identity or metadata. No further owner-level gap found.
- Retry safety: `CompleteEmailChallengeHandler` resolves an accepted retry through `Acceptance.Decide` and `PrepareReplayCompleteChallenge` (which has no verified check), so the new Conflict does not break exact-retry acknowledgement.
- Replay strictness: `Apply(EmailAddressVerified)` now throws on a duplicate verification. Any stream that already holds a duplicate (written before this guard) would fail to hydrate; no such data is expected pre-release, recorded as a risk.
- Tests: each new test asserts refusal kind, unchanged pending-fact count and unchanged state, and each fails without its guard. Fixture values (64-char hashes, future expiry, key id) pass `PrepareIssueRecoveryChallenge` validation, so the Conflict comes from the id guard and not from validation. Names and Arrange/Act/Assert phases follow CNTRYL0001/0002. No changes to tests or source were needed.
- Compile read: helpers `Reserve`, `IssueChallenge`, `CompleteChallenge`, `IssueRecovery` resolve to the `EmailAddressFacts` extensions (usings present); `Error!.Kind` used after `Assert.False`; `Assert.Single` and `.Count` on list; replay test builds history on one seed owner and calls `NativeFacts.Raised` once. Not confirmed by compiler.

### Discrepancies

None: no event or command differs from the catalog, no naming conflict, no payload field repeats stream identity, no owner decision is silent. No event changes needed.

### Jev (model jev-1.14.0, jev-latest), advisory

Files: `implementation-jev/emailaddress-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
|---|---|---|
| Second verification of verified address | conflict, no fact | 1.00 |
| Recovery challenge id reuse after expiry | conflict, no fact | 1.00 |
| Tests assert the invariant and fail without the guards | true | 0.90 |
| Refusals need no new event or catalog entry | true | 0.89 |

All answers decisive and consistent with the applied code.

### Remaining risks

- Build, analyzers and tests are not run; RED/GREEN not observed.
- Replay test depends on `NativeFacts.Replay` on a fresh `EmailAddress(string)`; same pattern as `IdentityRecoveryOwnerTests`.
- Catalog `implementationStatus` and `baselineSourcePath` are stale (catalog owner).


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
