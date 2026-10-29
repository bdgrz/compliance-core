# ArtifactEngagementHold aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together** (catalog `boundaryDecision: retain`). One hold (`ArtifactEngagementHold`, stream `{tenant}/artifact-engagement-holds/{holdId}`) decides atomically that an engagement preservation hold cannot release before one year after its exact report issuance.

- Identity: tenant plus the caller-supplied immutable `HoldId` (creation identity; exact retry returns the original result, changed intent is refused). The address derives from tenant and hold id only; no other caller key forks a stream.
- Invariant: release is refused until `ReleaseEligibleOn` (issuance date plus one year); report identity and the release decision are immutable. Catalog 2026-10-29 revision adds: release also requires the attributed delivery decision (D27-05).
- Exclusions: legal holds (`ArtifactLegalHold`), retention basis, the delivery decision's own provenance and the offboarding/disposition gates read from this hold, report artifact storage, and handler/authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Place (valid) | placed, active, revision 1 | `ArtifactEngagementHoldPlaced` |
| none | Place (invalid) / Issue / Release | validation or not-found, no event | none |
| active, no report | Place exact retry | success, no event | none |
| active, no report | Place changed | conflict | none |
| active, no report | RecordReportIssued (expected revision matches) | issued, eligible on = date + 1 year | `EngagementReportIssued` |
| active, no report | RecordReportIssued stale revision | conflict | none |
| active, no report | Release | conflict (no eligibility date) | none |
| active, issued | Issue exact retry (revision <= current) | success, no event | none |
| active, issued | Issue other artifact/reference | conflict | none |
| active, issued | Place exact retry | success, no event | none |
| active, issued | Release before eligibility | conflict | none |
| active, issued | Release (eligible, revision matches) | released | `ArtifactEngagementHoldReleased` |
| active, issued | Release stale revision | conflict | none |
| released | Release exact retry / changed | success / conflict | none |
| released | Issue exact retry | success, no event | none |

## Changes

- Gap fixed (identity retry): `Place` compared the retry's `ReportArtifactId` with the current view. A hold placed without an artifact id and later issued had its exact placement retry refused as a conflict. The placed artifact id is now remembered from the placement fact (`_placedReportArtifactId`, set on replay too) and used for the retry comparison.
- Gap fixed (concurrency): catalog `RecordEngagementReportIssued` carries `ExpectedRevision` (added 2026-10-29, `require_expected_revision`). Added overload `RecordReportIssued(long expectedRevision, ...)`: stale revision is a conflict with no event; an exact retry of the committed issuance succeeds with an older revision. The original overload remains and delegates with the current revision, so the existing handler still compiles; the handler should move to the new overload (follow-up).
- No other defect found. Apply guards re-validate each fact (revision continuity, actor, derived eligibility date).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Place, active until report, one-year expiry | `ShouldRetainIndefiniteHoldUntilReportIsIssuedGivenPlacement`, `ShouldKeepHoldActiveThroughOneYearAfterReportGivenReportIssuance` (existing) |
| Early release refused, release and replay | `ShouldRequireElapsedHoldAndPreserveReleaseGivenRelease` (existing) |
| Changed placement / second report refused | `ShouldRejectChangedHoldIdentityAndSecondReportIssuanceGivenRetry` (existing) |
| Exact placement retry after later facts | `ShouldAcceptExactPlacementRetryGivenReportIssuedAfterPlacementWithoutArtifact` (new) |
| Invalid decisions append nothing | `ShouldAppendNothingGivenInvalidPlacementOrIssuance`, `ShouldRefuseReleaseGivenNoReportIssuedAndRejectInvalidRelease` (new) |
| Stale revision, issuance retry | `ShouldRefuseStaleIssuanceAndAcceptOlderRevisionRetryGivenExpectedRevision` (new) |
| Stale release, immutable release, retry after release | `ShouldRefuseStaleReleaseAndKeepReleaseImmutableGivenReleasedHold` (new) |
| Valid replay, retries on replayed state | `ShouldRestoreReleasedHoldAndAcceptRetriesGivenValidHistoryReplay` (new) |
| Competing decisions | expected-revision tests above; the single revision line decides |

Malformed-history tests were not added, by user preference.

## Event contract status

- `ArtifactEngagementHoldPlaced`, `EngagementReportIssued`, `ArtifactEngagementHoldReleased` replay. They carry the full `ArtifactEngagementHoldView` as state; catalog `eventAuthority` is "retained_baseline_business_facts_requires_schema_reconciliation". No change applied.
- Event change needed (not applied): the 2026-10-29 invariant requires a delivery fact. The catalog lists the receiver `RecordEngagementPackageDelivered` (PLANNED; fields CommandId, Source, Target, OperationId, Generation, ExpectedRevision, EngagementId, PackageId, DeliveryDecisionId) but no event for it. Add `EngagementPackageDelivered` (hold id, engagement id, package id, delivery decision id, attributed actor, delivered-at) to the catalog, schemas and JSON context. Then the aggregate gains `RecordPackageDelivered` and `Release` refuses without it. Until the event exists, D27-05 release gating is an OPEN ITEM, so the aggregate stays partial.

## Later-stage follow-ups (not aggregate gaps)

- `RecordEngagementReportIssuedHandler`: pass the command's `ExpectedRevision` to the new overload.
- Reactor/receiver for `RecordEngagementPackageDelivered` fed by the delivery decision; offboarding/disposition gates reading hold status.
- Release handler authorization, server-time selection for placement.

## Evidence

- Jev: no request this round. The boundary and gaps rest on the catalog entry and code.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArtifactEngagementHoldTests"`, then the unit suite.
- RED: none captured; not claimed.
- No host, durable-store or production qualification is claimed.

## Review before build (independent reviewer, 2026-10-29)

Evidence: written, not executed. No build, test or format run.

### Findings and fixes

- Tests (fixed): `Assert.Equal(1, <array>.Length)` forms in two tests risked xUnit2013; replaced with comparisons against a captured count. Invalid-decision and stale-release tests ran refusals inside Arrange; moved them to Act so the phases are honest. The invalid-decision test now also asserts exactly one fact (the valid placement) after all refusals.
- Stale-release test now also asserts the first release decision (rationale) survives the changed-release attempt.
- Implementation: no correctness gap found in the aggregate. Place retry uses the remembered placed artifact id (also on replay); issuance retry is acknowledged without an event; stale first issuance and stale first release conflict without an event; released holds keep the decision immutable; replay re-validates revision continuity, actor and eligibility date.
- Compile risks checked: usings unchanged (NativeFacts via Bdgrz.Compliance.Tests.Testing, RequestErrorKind via Cntryl.Portia); overload arities (5 vs 6 args) are unambiguous; the existing handler call still binds to the transitional overload; Raised returns an array so `.Length` is correct; Replay takes the single Raised copy; events are registered in ComplianceCoreJsonContext so the Raised JSON round trip works. Not confirmed without a build: CNTRYL/CA analyzer results on the new tests.
- Payload note (not changed): events carry the full view as state, which repeats tenant and hold id from stream identity and metadata. Catalog `eventAuthority` already flags this as requiring schema reconciliation; left to the shared-file owner.

### Jev (model jev-1.14.0; request and response in implementation-jev/artifactengagementhold-test-impl-review-2026-10-29-*.json)

| Question | Answer | Probability |
|---|---|---|
| Delivery fact missing from hold stream: disposition | add_to_model | 0.91 (confidence 0.86) |
| Premise: hold cannot enforce the D27-05 gate without a durable delivery fact in its own stream | true | 0.93 |
| Issuance retry/stale handling consistent with replay rule | consistent | 0.94 |
| Tests assert behavior and can fail | true | 0.94 |

Disposition: the delivery event is decisively a catalog gap. Catalog/schema/JSON context changes are not applied (shared files). Catalog note: choreography X007 lists receiver `RecordEngagementPackageDelivered` with `emittedEventContracts` empty; the hold needs its own delivery fact (for example `EngagementPackageDelivered`) carrying engagement id, package id and delivery decision id, with the source `ClientExportAttestPackageDeliveryRecorded` as the cause. Aggregate stays partial until then.

### Remaining risks

- D27-05 delivery gate on Release unimplemented (needs event).
- Tests unexecuted.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
