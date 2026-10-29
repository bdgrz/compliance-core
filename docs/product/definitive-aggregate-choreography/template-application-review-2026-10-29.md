# TemplateApplication aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together.** One application (`TemplateApplication`, stream `template-applications/{applicationId}` under the tenant) decides atomically its pinned published template provenance, planned draft identities and completion state, and the one-pending-successor-proposal rule.

- Identity: `TemplateApplicationId.For(programId, templateId, trimmed ApplicationKey)` (UUIDv5). `Validate` rejects any fact whose header does not derive the stream id, so no caller key forks a stream. Per-entry draft ids remain UUIDv5(applicationId, entryKey).
- Invariant: one application pins exact template version, content hash, requester and planned draft set (immutable after the first fact); one pending later-version proposal receives one immutable disposition before the next successor; revisions are strictly sequential.
- Exclusions: template publication and review (MethodologyTemplate), the tenant drafts created from entries (their own aggregates), lifetime proposal/decision history (projection from content refs; the aggregate keeps only compact header admission), authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Apply (revision 1, requester = actor, no pending successor) | applied | `MethodologyTemplateApplied` |
| applied or drafts_created, no pending | Propose successor (later version, expected revision) | pending successor | `TemplateSuccessorAdoptionProposed` |
| pending | Propose another successor | conflict, no fact | none |
| pending | Record disposition (expected revision) | pending cleared | `TemplateSuccessorDispositionRecorded` |
| no pending | Record disposition | conflict, no fact | none |
| any | stale expected revision or stale prepared position | conflict, no fact | none |
| any | exact retry of committed command | original result (operation layer), no fact | none |

Draft-creation receipts change `CreatedDraftCount` and status through the same header admission.

## Changes

None to the aggregate. Adversarial review found the owner already enforces identity derivation, immutable original intent, sequential revision, operation/actor/time binding, immutable content digest binding and successor ordering. Only test gaps were found.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, creation pins version | `ShouldPinOneApprovedVersionAndCreateOnlyDraftProvenanceGivenApplication` |
| Successor ordering, exact retry, new proposal after disposition | `ShouldRequireExactPublishedSuccessorAndRetainTenantDispositionGivenTemplateUpdate` |
| Competing proposal refused, no fact | `ShouldRefuseSecondProposalWithoutFactGivenPendingSuccessor` (new) |
| Disposition without pending refused, no fact | `ShouldRefuseDispositionWithoutFactGivenNoPendingSuccessor` (new) |
| Stale revision refused, no fact | `ShouldRefuseProposalWithoutFactGivenStaleExpectedRevision` (new) |
| Changed-intent refusal after commit | `ShouldRejectChangedNativeBindingGivenHistoricalLogicalIdentity` |
| Exact retry, original actor display preserved | `ShouldPreserveOriginalActorDisplayGivenRenamedRequesterRetry`, `ShouldReplayApplicationAfterDraftReceiptGivenLaterClockAndDisplay` |
| Replay of valid history before content reads | `ShouldReplayBeforeApplicationHistoryReadsAndClassifyFreshReadGivenUnreadableContent` |
| Unbound prepared content refused | `ShouldRejectUnboundPreparedContentGivenNativeApplicationAppend` |
| Bounded history | `ShouldContinueSuccessorDecisionsGivenMoreThanFormerLifetimeLimit`, `ShouldKeepCompactFramesAndCompleteHistoryGivenSeventySuccessorDecisions`, `ShouldFreezeFullLargeApplicationAndOriginalResultGivenLegalContentBeyondEventFrame` |
| Tenant/program isolation | `ShouldHideForeignProgramAndTenantGivenScopedApplicationRead` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `MethodologyTemplateApplied`, `TemplateSuccessorAdoptionProposed`, `TemplateSuccessorDispositionRecorded` carry a compact header, an immutable content reference (full body outside the frame), first response, operation result and operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handler/route authorization precedence before replay; draft-creation reactor wiring; application and proposal history projections.

## Evidence

- Jev: no request this round; catalog entry and existing code were sufficient and no discrepancy was open.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~TemplateApplication"`.
- RED: none captured; not claimed. The three new tests characterize existing behavior and are expected green.
- Residual risk: new tests assert only `IsSuccess == false` and fact counts since the failure kind at the operation layer was not confirmed; uncompiled.

## Review before build

Independent review, nothing compiled or run.

Findings and fixes:
- The three new tests asserted only `IsSuccess == false`. Traced `TemplateApplicationRules.ProposeSuccessor`/`RecordSuccessorDisposition` and `TemplateApplicationOperations`: competing proposal and disposition-without-pending return `StateConflict`, a stale expected revision is rejected in the operation layer with `Conflict`. Fixed by adding `Assert.Equal(RequestErrorKind.Conflict, x.Error!.Kind)` to each (`RequestErrorKind` via the existing `Cntryl.Portia` using).
- Argument order of `ProposeTemplateSuccessorAdoption` and `RecordTemplateSuccessorDisposition` matches the existing tests; versions use `Version(n)`, ids are valid.
- Aggregate `Validate` checked for the invariant: pending-flag transitions, sequential revision, immutable original intent, identity derivation. No defect found; source unchanged.

Jev (jev-1.14.0), files `implementation-jev/templateapplication-test-impl-review-2026-10-29-*.json`:
- Competing proposal: refuse_conflict_no_fact 1.00.
- Disposition without pending: refuse_conflict_no_fact 1.00.
- Tests assert the invariant (Noul): 0.88.
No catalog/code discrepancy found, so no Choice on events was needed.

Remaining risks: uncompiled; the aggregate's own `Validate` refusal for these cases is shadowed by earlier rule/operation-layer refusals, so these tests prove command-level behavior, not the aggregate guard directly.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
