# PlatformOperatorRoster aggregate review, 2026-10-29

Scope is the roster aggregate and its events. This is a source and existing-test review; no implementation or test changes were needed. Focused tests and independent Jev review are pending.

## Identity, invariant and boundary

The fixed `bdgrz/platform-operator-roster/bc279ad5-e9ae-5033-a174-4d1350ce2b07` stream owns one authoritative current set of operator user IDs and assignment episodes. The named invariant is that each grant or revocation is decided by a current operator against the exact roster and a revocation cannot remove its last member. `m0-d25-client-tenancy.md` further requires recorded actor, subject, time and reason; configuration initializes only an empty roster, and later configuration must never regrant a revoked operator.

Keep the complete current set in one decision boundary: independently writing per-user grants without a coordinating admission authority would let simultaneous removals violate the last-operator rule or let a removed actor grant access. The owner stores a digest-verified immutable authority reference and compact count/revision, and re-evaluates the full prepared authority, exact assignment and current actor in `PlatformOperatorRosterRules.Apply` before accepting an append. The count is not used as a substitute for exact identities. Physical OCC and the prepared position/revision serialize competing decisions; no roster membership cap is imposed. Access-control projections and user existence checks remain outside the roster, while the owner itself refuses an absent acting operator.

## Lifecycle and event contracts

| State | Decision | Outcome |
|---|---|---|
| Absent | Bootstrap fixed identity with canonical nonempty configured users | `PlatformOperatorRosterSeeded`; current exact assignments initialized |
| Initialized | Grant by current operator for inactive subject at expected revision | `PlatformOperatorGranted`; assignment episode bound to command ID |
| Initialized | Revoke exact subject assignment by current operator at expected revision, with at least one other operator left | `PlatformOperatorRevoked` |
| Any initialized state | Seed again, stale/changed assignment or revision, inactive actor, duplicate grant, last removal | No new fact; refusal or previously accepted result as applicable |
| Accepted operation | Exact authorized retry | Native acceptance resolves original result; changed binding conflicts; no new fact |

All three event names are in the selected catalog and code. The events contain native provenance, immutable authority/change references, and original response material; independent Jev review should consider any duplicated identity fields and report a versioned-contract recommendation rather than changing persisted discriminators. The application verifies the full immutable authority before mutation and binds both prepared authority and outcome to the emitted event. History growth and external content retention are separate operational costs, not a reason to add a cap or split the invariant.

## Existing behavioral evidence

| Criterion | Existing `PlatformOperatorRosterTests` coverage |
|---|---|
| Last operator refusal and competing appends | `ShouldRejectLastOperatorRevocationGivenSingleActiveOperator`, `ShouldRetainOneOperatorGivenConcurrentCompetingRevocations` |
| Exact retry after later grant/revocation and changed intent | `ShouldNotRestoreAuthorityGivenAnExactGrantRetryAfterRevocation`, `ShouldPreserveLaterAssignmentGivenAnExactOldRevocationRetry`, `ShouldRetainOriginalNativeUnitResultGivenExactRetryAfterLaterProgress`, `ShouldConflictGivenChangedNativeBindingUnderAcceptedGrantIdentity` |
| Delayed old assignment and duplicate subject | `ShouldPreserveNewAuthorityGivenDelayedRevocationOfAnOldAssignmentAtTheCurrentRevision`, `ShouldRejectNewGrantIntentGivenAnAlreadyActiveSubject` |
| Seed and restart semantics | `ShouldNotRestoreRevokedOperatorGivenRepeatedConfigurationSeed`, `ShouldInitializeOnceGivenConcurrentHostSeeds`, `ShouldKeepTheInitialSeedGivenChangedLaterStartupConfiguration` |
| Original actor, subject, time, reason | `ShouldRecordActorDisplaySubjectTimeAndReasonGivenANewGrant`, `ShouldDenyBeforeResolvingAcceptedIntentGivenAnActorWhoLostOperatorAuthority` |
| Valid replay and uncertain append recovery | `ShouldRecognizeOriginalNativeIntentGivenConcreteDurableRoundTrips`, `ShouldRecoverAcceptedIntentGivenAnUnknownOwnerAppendOutcome` |
| Authority tampering and availability | `ShouldRejectChangedPreparedAuthorityGivenAnOtherwiseValidNativeAppend`, `ShouldFailClosedGivenUnavailableOriginalAuthorityOrResponseOnRetry` |
| No artificial small roster cap | `ShouldKeepAcceptingFactBoundedGivenLargeLegalBootstrapRoster` exercises 4,096 bootstrap users; it is not proof of arbitrary size or production capacity. |

These tests have not been rerun in this review. Intended focused run: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PlatformOperatorRosterTests'`. No RED claim: source review found no concrete defect. The concurrency and recovery tests use controlled in-memory components, not a production broker or durable store. Existing legacy roster facts without trusted native authority are intentionally rejected (`ShouldFailClosedGivenRetainedLegacyRosterWithoutTrustedNativeAuthority`); conversion/migration and production compatibility need separate qualification. Hosted CI and full-suite results must be reported independently.

## Independent review before build

`Append` checks the exact hydrated revision and physical position, validates the supplied prior authority against the committed immutable reference, re-evaluates the actor and exact assignment against that full roster, and binds the resulting authority, mutation, fact and original response. The current action path uses those checks, with framework OCC for competing append attempts. Existing controlled tests exercise the last-member refusal, simultaneous removals, retry after later progress, changed binding, bootstrap restart, unavailable immutable authority and unknown append outcome; they were not rerun by this review.

All three event names are in `events.json` and the aggregate's selected owner entry. The occupied seeded v1 contract nevertheless has a material compatibility boundary: `PlatformOperatorRosterSeeded.UserIds` held the initial operator IDs in legacy facts, while the current `PlatformOperatorRosterContent.Fact` writes an empty array and puts the IDs in a referenced immutable authority. The current reducer rejects legacy facts missing trusted native authority and receipt. A legacy consumer reading only `UserIds` also sees no users in a new fact. Whether such facts or consumers exist in deployed environments is not established; a versioned cutover or trusted conversion and consumer migration must be qualified separately before claiming compatibility. No event or schema change was made here.

Jev `jev-1.14.0` assigned 0.96 to rejection of legacy facts, 0.97 to the changed v1 seed field, and 0.85 to a qualified versioned cutover versus 0.15 source resolution and 0.00 declaring v1 compatible. Its score for the last-operator question was 0.26; the source does verify the full authority and exact actor/assignment before counting the resulting set, but that question conflated use of a count with relying on a count alone, so its answer is not evidence against the code. Exact old-grant retry not regranting scored 0.76. The payload duplication premise scored 0.90 on the second request; qualified versioned change 0.66, source resolution 0.25 and retain current contract 0.09 do not qualify any concrete migration. Requests and responses are in `implementation-jev/platformoperatorroster-{test-impl-review,payload-review}-2026-10-29-{request,response}.json`. Jev is advisory.

Status remains partial for occupied-v1 legacy replay and consumer compatibility; the existing rejection test demonstrates fail-closed behavior, not a completed migration. Focused test, format, full-suite, hosted CI and production evidence remain separately pending.

## Wave 10 coordinator validation (2026-10-29)

Release compilation and the contribution, partner decision, roster and acknowledgement selection passed 115 tests. The full non-broker, non-web suite passed 7,887 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The suite is not green. Checked-in producers were reviewed; persisted histories, legacy wire migration and production behavior remain unqualified.
