# ServiceEngagementIndependence aggregate review, 2026-10-29

Scope: aggregate and domain-event stage only. Tests are written, not executed; build and run pending.

## Identity and boundary

One stream per tenant and engagement holds draft lifecycle, the current content revision and the admission/settlement of acceptance, assignments and personal decisions. Keep these facts together because each admission pins the same engagement revision and must serialize against mutations. Firm-wide permanent practice walls belong to `PracticeIndependenceCommitment`; rule, staff and client service proofs come from their respective owners and are checked in the ordered admission process. Reactors coordinate those distinct owner decisions; they do not substitute projection checks for the underlying commitments. Creation's logical ID is the engagement ID; later command IDs select operation acceptance and do not change the stream key.

## Lifecycle and guards

| State | Action | Result |
|---|---|---|
| none | create at revision zero using the engagement ID | draft, revision 1 |
| draft | amend, propose, withdraw or close at current revision without pending pins | revision advances; close terminates draft |
| draft | admit personal decision or acceptance at exact frozen revision | pending pin; mutation is blocked until settled |
| draft | authorize acceptance after source and commitment proof, then record | accepted, revision advances |
| accepted | admit and record staff assignment, revoke assignment or close accepted engagement | revision advances as recorded |
| any | stale revision, wrong owner, wrong state, competing or pinned operation | failure without a new fact |

Accepted facts and results are selected by logical ID on hydration for exact retry; changed actor/intent/revision is refused in the admission path. Permanent Advisor/Attest conflicts and impaired evaluations are enforced via the admission and commitment process, not by interpreting a mutable projection here.

## Changes and test mapping

The reducer refuses unsupported lifecycle fact kinds and a creation whose operation ID differs from the engagement ID, but the direct `Change` path could reach `RaiseEvent` with those values. The owner now validates both before a fact is raised. `ServiceEngagementIndependenceOwnerTests` adds refusal/no-fact coverage for the wrong creation ID and the unsupported lifecycle kind. The latter tests `PrepareChange`; `Change` calls that guard before raising.

| Criterion | Existing or new evidence |
|---|---|
| Wrong creation identity; unknown lifecycle kind | new direct owner tests |
| Exact retry after later change; changed intent/actor/revision | `ServiceEngagementIndependenceTests.ShouldReturnOriginalViewAndAppendNothingGivenExactRetryAfterLaterChange` and `ShouldConflictWithoutAppendGivenSameCommandIdentityWithChangedIntentActorOrRevision` |
| Lifecycle, stale revision, closure, concurrent creation | existing `ServiceEngagementIndependenceTests` |
| Acceptance/decision pins, retry and authorization fences | existing `ServiceEngagementAcceptanceTests`, `EngagementAcceptanceProcessTests`, and `EngagementPersonalDecisionTests` |
| Impairing service blocks acceptance and permanent practice separation | `IndependenceCompartmentsTests` and engagement acceptance/assignment tests; review proof integration separately |

Tests are unrun and were written before production changes without an executed RED. The broad process tests above were not rerun in this phase. In-memory pending operations are removed on terminal outcomes, while replay history can continue to grow with engagement changes; no application-level history limit is introduced without a source-backed requirement.

## Event contract and follow-ups

The selected catalog lists `AssignmentIndependenceReevaluated` and `ServiceIndependenceReevaluated` under this owner, but the current aggregate does not emit or fold either legacy record. Their source definitions remain present. This discrepancy needs independent Jev and source review before declaring their disposition or changing a shared catalog/schema. Other selected native events carry shared operation envelope fields such as owner and revision; the independent reviewer should evaluate duplication against the shared protocol. No event contract was changed in this phase.

Source validation, authorized retries, and cross-owner proof coordination remain later-stage responsibilities. No Jev request was made by the implementer; independent review follows. No build, test or formatting has run; the tests and source change are uncompiled.

## Independent review before build

The source, changed guards, direct tests, selected catalog and legacy event definitions were reviewed without execution. The creation guard matches the replay reducer's `OperationId == EngagementId` rule and prevents the invalid event from reaching it. `Change` invokes `PrepareChange` before raising, so its unknown-kind path is covered by the direct guard test. Both new tests assert a failure and no pending event; neither has been run. No further code edit was made.

The missing receipt discrepancy is substantive. `events.md` assigns both reassessments to this owner, and `target-event-contracts.md` selects externalized content, operation provenance and owner revision instead of the legacy records' inline content and shared-ledger sequence. The legacy types are only referenced by their declarations and JSON context in current source; this aggregate emits or folds neither. The assignment product document describes an attributed denial-only causal receipt for removal/closure; the service document describes a retained source-change assessment. A current epoch guard cannot by itself prove either retained receipt. The required owner behavior, versioned event contracts, historical compatibility and source/receipt coordination remain open; do not mark this aggregate done. No event schema was changed in this parallel phase.

Jev `jev-1.14.0` is advisory; inputs and complete responses are in `implementation-jev/serviceengagementindependence-test-impl-review-2026-10-29-{request,response}.json`. Live TypeSafe docs were inaccessible, so the request shape used a retained working repository example.

| Question | Result | Disposition |
|---|---|---|
| Assignment receipt disposition | implement target 0.46, source resolution 0.46; premise 0.56 | Near-even; unresolved, preserve selected catalog and report missing behavior/contract |
| Service receipt disposition | implement target 0.69, source resolution 0.27; premise 0.55 | Premise is uncertain; source-backed catalog remains authoritative, report missing behavior/contract |
| Wrong creation identity refusal | yes 0.84 | Consistent with catalog and replay guard |
| Repeated tenant and operation owner | source resolution 0.54, retain 0.30, versioned change 0.16 | Unresolved; no unilateral event change |

Coordinator should run focused tests and full suite after batch review and address the two selected event/behavior gaps as a single-writer contract change or record them as partial. The proof integration, handler/process trigger and actual OCC/race coverage were not established by this static review.

## Root provenance regression

The coordinator's focused RED run exposed accepted invalid root provenance before the fix. `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FirmStaffIdentityClaimTests|FullyQualifiedName~FirmStaffRecordAggregateTests|FullyQualifiedName~ServiceEngagementIndependenceOwnerTests'` returned 37 failed and 14 passed across the combined selection (51 total). A fresh `Change` could append a fact whose routing owners, causation, episode, digest or accepted time caused `ServiceEngagementHistory.ReadAsync` to reject the committed history, blocking subsequent verified operations. `Change` and the replay reducer now apply the existing `NativeOwnerHistory.IsOwnRoot` rule at the current predecessor revision. The change applies to all five lifecycle kinds; `ServiceEngagementCommands.RunAsync` constructs compatible root provenance. The new focused tests cover a valid creation and invalid routing owners, causation, episode, digest and timestamp, asserting refusal with no fact. Green verification remains pending.

## Coordinator validation (2026-10-29)

The Release test-project build passed with zero warnings and errors. After the provenance fixes, the four-owner focused selection passed 57 tests; the related staff and engagement selection passed 118 tests. The full non-broker, non-web suite passed 7,798 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks. Those same five failed on hosted develop before wave 2; the suite is not green. Independent source review found the normal operation builders satisfy the new guards. No durable-store, hosted CI, or production qualification is implied.

Whitespace verification passed for the four owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
