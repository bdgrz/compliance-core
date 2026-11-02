# FirmStaffIdentityClaim aggregate review, 2026-10-29

Scope: aggregate and domain-event stage. Tests are written, not executed; build and run pending.

## Identity, invariant and boundary

Keep the platform-wide claim together: the stream keyed by canonical user serializes one permanent mapping to a staff record. Status changes on `FirmStaffRecord` do not release this claim. The selected logical ID selects acceptance during hydration and does not change the stream key. Practice and source are frozen in the registration intent, and source verification and exact receiver replay belong to the registration receiver, not this claim's direct method. A reactor sends the source owner's admitted registration to this owner; it does not decide uniqueness itself.

## Lifecycle

| State | Decision | Outcome |
|---|---|---|
| Unclaimed | Valid own-stream claim for a nonempty staff identity | One `FirmStaffIdentityClaimed` at revision 1; permanently claimed |
| Unclaimed | Foreign owner or empty staff identity | Validation failure, no fact |
| Claimed | Any direct claim | Permanent conflict, no fact |
| Claimed, receiver exact replay | Receiver resolves the selected accepted operation | Original fact, no new claim |

## Changes and coverage

The replay reducer already rejects an empty root operation ID, which represents the staff identity, but `Claim` previously allowed that ID to reach `RaiseEvent`. It now returns validation before emitting a fact. This prevents a command from producing a fact its own reducer refuses. No other source change was made.

| Criterion | Evidence |
|---|---|
| Canonical-user key, independent of selected operation | `ShouldKeepClaimIdentityStableGivenCanonicalUser` |
| Foreign owner and empty staff identity refuse without a fact | `ShouldRefuseClaimWithoutFactGivenForeignOwnerOrEmptyStaffIdentity` |
| Successful claim, valid replay and competing claim without a fact | `ShouldPreserveClaimAndRefuseCompetingClaimGivenValidReplay` |
| Receiver exact replay, changed intent, authorization, one fact | Existing `FirmStaffRecordTests` receiver tests; not independently rerun here |
| Actual concurrent append serialization | Portia stream concurrency and receiver retry path; no new aggregate-level competing append test |

The claim accepts a single fact, so valid aggregate history has a maximum of one fact. The catalog lists `FirmStaffIdentityClaimed`, matching the source. Its `Owner`, `Revision`, `Generation`, `Intent`, and `Operation` are part of the shared native-operation event envelope; potential envelope duplication merits independent Jev review. No event change was made or requested at this stage.

## Later-stage follow-ups and evidence limits

Source proof, practice matching, authorization and replay binding are receiver/process responsibilities; no handlers or reactors were changed. Tests and build have not been run in this phase. RED was not observed before the production fix because the batch workflow defers execution; the empty-root regression test is written first but not run. No Jev request was made by the implementer; independent review follows. The new tests and code are uncompiled.

## Registration child provenance follow-up

The independent review found that the direct claim path could append provenance that `FirmStaffHistory.VerifyClaimAsync` later refuses. The owner now refuses an empty canonical user or logical operation ID, and checks the registration child's actor kind, command kind, causation presence, staff first owner, affected and revision owners, and original revision before raising a fact. The receiver still verifies the actual source and frozen intent; these checks do not replace that proof. The command's normal receiver constructs this provenance using `FirmStaffEffects.Child` from the verified registration.

The direct test fixture now represents a registration child instead of a root operation. New regression tests check empty user and invalid child provenance fields for validation refusal without a fact. The coordinator ran the regression selection before this guard and observed a RED for an accepted invalid child; the combined run reported 37 failures and 14 passes across the wider selection. Green and exact final validation remain pending.

## Independent review before build

The source, direct tests, native-fact helper and receiver were reviewed without executing them. The empty-root guard matches the existing reducer's nonempty root check. The receiver checks the source, matching practice and actor-bound acceptance before it resolves a replay, and catches stream concurrency to rehydrate and decide again. The direct aggregate's conflict after an existing claim is therefore distinct from the receiver's original-result replay. The replay test asserts both permanent staff ownership and no second pending fact; the new refusal test checks empty root and foreign owner. The only edit during review was adding a nullable suppression on the `Error` assertion to avoid a possible warnings-as-errors diagnostic.

Jev `jev-1.14.0` is advisory; exact request and response are in `implementation-jev/firmstaffidentityclaim-test-impl-review-2026-10-29-{request,response}.json`. Live documentation was inaccessible; request structure was checked against a retained working repository request.

| Question | Answer | Probability | Disposition |
|---|---|---:|---|
| Does replayed claim preclude a different staff identity? | yes | 0.90 | Signal consistent with direct code and test |
| Is direct conflict compatible with receiver original-result replay? | compatible_receiver_replays | 0.98 | Consistent with receiver code; no change |
| Repeated owner field in native event | retain_shared_envelope | 0.98 | Keep existing shared contract; no unilateral wire change |
| Does the event repeat owner in two fields? | yes | 0.93 | Confirmed from event type; retained as shared envelope |

No event addition, removal or rename is requested. Reviewer did not run build or tests; the generated JSON replay and `AggregateScenario` behavior remain runtime checks. Actual concurrent append is exercised by the receiver's retry mechanism but was not newly tested here, so aggregate completion should not be asserted solely on the new tests.

## Coordinator validation (2026-10-29)

The Release test-project build passed with zero warnings and errors. After the provenance fixes, the four-owner focused selection passed 57 tests; the related staff and engagement selection passed 118 tests. The full non-broker, non-web suite passed 7,798 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks. Those same five failed on hosted develop before wave 2; the suite is not green. Independent source review found the normal operation builders satisfy the new guards. No durable-store, hosted CI, or production qualification is implied.

Whitespace verification passed for the four owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
