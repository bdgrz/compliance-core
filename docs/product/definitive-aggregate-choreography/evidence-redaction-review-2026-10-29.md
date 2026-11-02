# EvidenceRedaction aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep (narrow).** One redaction (`evidence-redactions/{RedactionId}` under the tenant) decides atomically that an approval targets the current exact original/derived preparation and is independent of its preparer unless an exact scoped waiver proof authorizes self-approval.

- Identity: `Tenant / evidence-redactions / RedactionId`, with exact source artifact/content/registration capsules. `CommandId` is the logical operation id (preparation and approval ids), never a stream key.
- Invariant: approval must name the current preparation id and revision at the current owner revision, with no approval already current; the approver is not the preparer unless a waiver value proof matches tenant, redaction, preparation, revision, action, beneficiary, independent approver and active window.
- Exclusions: the waiver (separate owner, enters as a value snapshot), artifact retention/access (original is never altered), source authentication and derived availability (prepared by the handler, passed as values), complete preparation history (read-side facts via `EvidenceRedactionHistory`; the owner keeps only the current preparation and approval).
- Role checks (Contributor prepares, Compliance Lead approves) are authorization at the handler stage, not aggregate state.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none or prepared or approved | Prepare, expected = revision, same sources as current, chronology ok | revision + 1, approval cleared | `EvidenceRedactionPreparedV2` (V1 legacy replayable) |
| prepared, no approval | Approve, expected = revision, exact preparation id/revision, independent or waived | revision + 1 | `EvidenceRedactionApprovedV2` (V1 legacy replayable) |
| any | stale revision, changed sources under existing lineage, chronology regression, wrong preparation or revision, already approved, self-approval without valid waiver, payload over 48 KiB | conflict, forbidden or validation | none |
| any | exact retry of a committed fact, including after later facts | original result | none (selected-command lookup in native history) |
| any | changed intent under same CommandId | refusal | none |

## Changes

No source change. The aggregate already owns identity, invariant, lifecycle and bounded owner state; existing tests (composition, native proof, native HTTP, lifetime, 100+ preparation history) cover native retry and replay. The real gap was aggregate-level coverage: refusals of the approval invariant and preparation conflicts were proved only through the composition fixture, and waiver scope rules and replay of valid prepare/approve history had no focused decision test. Added `EvidenceRedactionDecisionTests.cs` (new, aggregate-only).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Replay of valid history (prepare, approve) | `ShouldReplayPreparationThenIndependentApprovalGivenValidHistory` |
| Later preparation supersedes approval on replay | `ShouldClearCurrentApprovalGivenLaterPreparationInReplay` |
| Next revision accepted | `ShouldAcceptNextRevisionGivenSameSourcesAndChangedReason` |
| Stale revision, changed source, chronology; no fact | `ShouldRefusePreparationWithoutFactGivenConflictingState` |
| Invalid lineage/attribution; no fact | `ShouldRefusePreparationWithoutFactGivenInvalidDerivativeLineage` |
| Approval only of current exact preparation; stale revision | `ShouldRefuseApprovalWithoutFactGivenAnythingButCurrentExactPreparation`, `ShouldRefuseApprovalWithoutFactGivenNoPreparation` |
| One approval per preparation | `ShouldRefuseSecondApprovalGivenCurrentPreparationAlreadyApproved` |
| Independence of approver | `ShouldForbidSelfApprovalGivenPreparerWithoutWaiver`, existing `ShouldDenySelfApprovalWithoutAppendGivenPreparerAsLead` |
| Exact scoped waiver admitted / rejected | `ShouldAcceptSelfApprovalGivenExactScopedIndependentWaiver`, `ShouldForbidSelfApprovalGivenWaiverThatIsNotExactIndependentAndActive` |
| Exact retry, changed actor, replayed original view after later facts | existing `ShouldReturnOriginalAttributedPreparationGivenExactRetryAndDenyChangedActor`, `ShouldReplayOriginalPreparationViewAfterLaterPreparationGivenNativeHistoryBoundary`, `ShouldReplayOriginalApprovalViewAfterLaterPreparationGivenNativeHistoryBoundary` |
| Competing append / native cut | existing `EvidenceRedactionNativeProofTests` |
| Bounded history and payload | existing `ShouldKeepCommandOwnerBoundedWithCompleteHistoryGivenNativeHistoryBoundary`, `ShouldDenyBeforeAppendGivenCompleteSerializedPreparationExceedsPayloadBound` |

Malformed-history tests were not added, by user preference.

## Event contract status

`EvidenceRedactionPrepared(V2)` and `EvidenceRedactionApproved(V2)` carry tenant, redaction id, expected revision and the preparation/approval fact (business facts, including the waiver snapshot); no metadata duplication. Catalog `eventAuthority` says baseline events require schema reconciliation; V2 operation-result receipts are native-operation provenance. No add, rename, remove or payload change requested here.

## Later-stage follow-ups

- Handler: role gating (Contributor prepare, Compliance Lead approve, D16-10), waiver snapshot assembly and derived availability/position resolution.
- Projector: redaction history and view (`EvidenceRedactionHistory`).
- Catalog note: `productRulesNotYetInSource` lists D16-09 and D16-10; both are enforced at handler/authorization level, not in the aggregate.

## Evidence

Tests written, not executed; build and run pending. RED not observed (no source gap found, new tests are coverage). Jev not consulted; no open design decision. Status: done pending build.

## Review before build

Independent review of source and `EvidenceRedactionDecisionTests.cs` before any build. Nothing was compiled or run.

Findings and fixes:
- Compile risk fixed: `ulong position = cond ? 1 : 2` (int natural type, no implicit non-constant conversion to ulong) changed to `1UL : 2UL`.
- Coverage gap fixed: waiver theory lacked requester-equals-approver, requested-before-preparation and approved-after-decision cases; added. `beneficiary_is_another_member` previously also made approver equal beneficiary (two causes); it now uses a fresh member id so exactly one condition is violated.
- Source: no defect found. Approve/Prepare reject stale revision, wrong preparation or revision, repeated approval, chronology regression and self-approval without exact waiver; replay re-runs the same decision and rejects divergence. No payload field duplicates metadata. No event, catalog or schema change needed; no discrepancy (no unregistered event or command).
- Residual weakness: `Assert.Empty(scope.PendingEvents)` in the pure decision tests cannot fail because decisions never append; the error kind, unchanged state and revision assertions carry the proof. Append and retry behaviour stays covered by the existing native proof and composition tests.

Jev (model jev-1.14.0, advisory), artifacts `implementation-jev/evidenceredaction-test-impl-review-2026-10-29-{request,response}.json`:
- Waiver with approver equal to requester should be rejected: reject_is_correct 0.81 (accept 0.17). Clear lead, matches code and the new test.
- Second approval of the same preparation is a Conflict with no fact: 1.00.
- Tests assert the behavioral invariant and can fail: noul 0.94.

Remaining risks: usings and `DomainEventSeed.Attach` usage mirror the existing `Legacy` helper in `EvidenceRedactionTests.cs` (same arguments) but are unverified by the compiler; `AggregateScenario` wrapping a replayed owner mirrors existing tests. Catalog `eventAuthority` reconciliation remains with the coordinator.


## Coordinator build and test evidence (2026-10-29, wave 8)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7741 passed, 5 failed of 7746.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
