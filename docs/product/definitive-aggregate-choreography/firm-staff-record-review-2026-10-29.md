# FirmStaffRecord aggregate review, 2026-10-29

Scope is the domain-event/aggregate stage. Tests are written, not executed; build and test are pending. No handler, reactor, or projection changes were made.

## Identity and boundary

The platform `staffMemberId` selects the record stream; a selected logical operation does not alter that identity. The owner preserves the immutable registration intent and decides one current staff status revision. Keep registration and status together because status must refer to that exact registration and cannot advance while a source-use hold exists. The separate `FirmStaffIdentityClaim` serializes the canonical-user-to-staff/practice uniqueness rule; registration completion is coordinated by the source-claim process. Staff rosters are projections. Proof of the user/claim and interpretation of immutable registration/status content are admission/receiver responsibilities, not inferred from projections by this owner.

## Lifecycle and contract

| State | Decision | Outcome |
|---|---|---|
| fresh, expected revision 0 | accept root registration for this staff identity | `FirmStaffRegistrationAccepted`, pending |
| fresh, wrong owner, logical id, root, or revision | accept | refused, no fact |
| pending, exact owner/root and verified selected native cut | complete or reject | `FirmStaffChangeRecorded` at revision 1 or `FirmStaffRegistrationRejected` |
| pending, different owner/root | complete or reject | refused, no fact |
| pending or absent | status change | transient conflict or not found, no fact |
| registered, no active use and expected current revision | status change | `FirmStaffChangeRecorded`, revision incremented |
| registered, active use or stale revision | status change | conflict, no fact |
| registered | authorize use | a duty or engagement use authorization at the held revision |
| any owner state | settle exact use | settlement/fence; use protocol determines valid generation and receipt |

Native selected-operation acceptance and handlers return the original result on exact retries, including after later facts; changed intent is refused. Valid replay checks registration, lifecycle and revision in the owner, while selected native-cut verification fences appends. The operation and event contract includes native owner/provenance fields; whether any envelope fields are redundant requires independent Jev review before proposing a versioned event change. The catalog lists all seven owner events; none is added, renamed, or removed here.

## Changes and evidence

`AcceptRegistration` now explicitly refuses a root other than the staff identity, matching its own replay guard. `CompleteRegistration` and `RejectRegistration` now refuse a foreign owner or root before an append. This binds the completion/rejection to the selected registration; existing replay guards already reject an incorrect rejection root, but a completion previously did not test the root in its aggregate guard. Receiver proof verification remains outside this slice.

| Criterion | Evidence |
|---|---|
| Stream identity and selected operation independence | new `FirmStaffRecordAggregateTests.ShouldKeepRecordIdentityStableGivenStaffMemberId` |
| Invalid registration owner/root and stale revision append no fact | new direct aggregate tests |
| Foreign completion/rejection owner/root append no fact | new direct aggregate test; the test exercises refusal but the unverified native cut would also reject these calls before this change, so it is not isolated RED evidence for the completion/rejection guard |
| No status or use against absent registration | new direct aggregate test |
| Original registration and status retry after later change; changed intent; stale revision | existing `FirmStaffRecordTests.ShouldReturnOriginalAcknowledgmentAndAppendNothingGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameStaffMemberWithChangedIntentOrOperator`, `ShouldReturnOriginalViewAndAppendNothingGivenExactStatusRetryAfterLaterChange` |
| Valid registration completion, status replay, held use and settlement | existing `FirmStaffRecordTests`, `FirmStaffNativeHistoryTests`, `ProfessionalDutyChangeTests`, `EngagementPersonalDecisionTests`, and `SourceUseReservationsVerifiedHistoryTests` |

Initial tests were written without a RED run. Independent review then added verified-cut provenance regressions; the focused RED evidence and subsequent fix are recorded below. GREEN remains pending.

## Independent review before build

The original completion/rejection test did not isolate the new checks: its unverified replay cut could reject first. Added `ShouldRefuseCompletionAndRejectionGivenVerifiedCutForAnotherRootOrOwner`, which replays a valid accepted root, verifies the exact cut through `FirmStaffHistory.ReadAsync`, selects a new logical operation, and tests wrong-root and foreign-owner refusal. This scenario is written but uncompiled and unrun. Its fixture relies on `AggregateScenario` preserving the committed stream position and `NativeFacts.Raised` producing a fact usable by `FirmStaffHistory`; confirm at test time. The direct registration root test changes only root and exercises the new guard on a fresh cut. No source or event change was made in review.

Jev `jev-1.14.0` (advisory; request/response in `implementation-jev/firmstaffrecord-test-impl-review-2026-10-29-*.json`): the original terminal test isolates its new checks, Noul 0.04 (confirmed false; added the verified-cut test); root guard attribution premise Noul 0.47 (unresolved as a semantic judgment; source requires `NativeOwnerHistory.IsOwnRoot` and the new registration guard matches that existing replay requirement); event Owner duplication premise Noul 0.91 and disposition `keep_shared_envelope` 0.95 versus versioned change 0.02 (retained the shared contract; no event change proposed). Jev did not evaluate the new test execution. Live TypeSafe documentation could not be reached via the browser; the request used the repository's saved successful request schema and the exact retained response.

## Provenance regression and fix

Coordinator RED: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FirmStaffIdentityClaimTests|FullyQualifiedName~FirmStaffRecordAggregateTests|FullyQualifiedName~ServiceEngagementIndependenceOwnerTests'` produced 37 failures and 14 passes across all three owners. FirmStaffRecord root and child provenance cases demonstrated decisions accepted despite the history verifier later refusing the corresponding facts. Counts are for the combined selection, not this owner alone.

Registration now requires a nonempty staff identity and the same own-root and platform-operator criteria already enforced by `FirmStaffHistory`. Completion and rejection require the exact parent event, owner, child command kind and copied root actor, episode, expected revision and authored digest; child logical identity must be nonempty and distinct from the root. These criteria match the existing `FirmStaffHistory.Child` contract and `FirmStaffEffects.Child` callers. Verified-cut regressions include valid completion and rejection controls and independent variations of root and child fields; GREEN and full suite remain pending.

## Coordinator validation (2026-10-29)

The Release test-project build passed with zero warnings and errors. After the provenance fixes, the four-owner focused selection passed 57 tests; the related staff and engagement selection passed 118 tests. The full non-broker, non-web suite passed 7,798 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks. Those same five failed on hosted develop before wave 2; the suite is not green. Independent source review found the normal operation builders satisfy the new guards. No durable-store, hosted CI, or production qualification is implied.

Whitespace verification passed for the four owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
