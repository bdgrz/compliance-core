# ComplianceIncident review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One incident stream (`{tenant}/compliance-incidents/{incidentId}`, immutable program scope) decides atomically its chronology: record, revision, one assessment per version, the independent system-description decision and resolution.

- Identity: tenant + immutable `IncidentId` (the creation command identity, `op.LogicalId == Id`). Later commands use their own CommandId; no caller key forks the stream. The requested program must equal the prepared header program.
- Invariant: one incident version may be assessed once; the current-version description decision and required follow-up eligibility gate advancement; resolution requires every admitted follow-up completed. `detected_at` and `occurred_at` never change after the initial report.
- Exclusions: follow-up work content and completion (Work/CorrectiveAction proofs), system description content, risk history, authorization, directory/read projections. Immutable history, sources and required proofs live in content-addressed authority, not aggregate state (bounded: header, current content ref, sequence).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record (own operation, revision 1) | recorded | `ComplianceIncidentRecorded` |
| none | any other kind / foreign operation / expected revision on record | refusal | none |
| recorded | Record again | refusal | none |
| recorded/needs_review | Revise at expected revision | version + 1 | `ComplianceIncidentRevised` |
| recorded | Assess at expected revision | assessed | `ComplianceIncidentImpactAssessed` |
| assessed | Description decision (system process, no expected revision) | gate recorded | `ComplianceIncidentSystemDescriptionDecisionRecorded` |
| assessed | Resolve (follow-ups complete) | resolved | `IncidentResolutionRecorded` |
| any | stale sequence/position | transient conflict | none |
| any | foreign program scope | conflict | none |

## Changes

No behavior gap found in the aggregate. `Append` already refuses foreign program scope, stale native sequence or stream position, facts that differ from the prepared mutation, wrong command kind or provenance, oversized envelopes, and any transition that rewrites identity, chronology or the original revision gate; reducer replay re-validates. Handler-level coverage (retry after clock moves, changed intent, stale revision, assessment-once, resolution proofs) already exists in `ComplianceIncidentOperationTests`. Missing was owner-level coverage of Append refusals, which is added.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, revision one, program scope | `ShouldAppendInitialFactAtRevisionOneGivenOwnIncidentOperation` |
| Invalid decision appends no event: foreign program | `ShouldAppendNothingGivenMutationPreparedForAnotherProgram` |
| Stale revision / competing append | `ShouldRefuseTransientlyGivenStaleSequenceWhenAppending`; handler: `ShouldRejectStaleRevisionGivenNewAssessCommandId` |
| Wrong command kind / expected revision on initial | `ShouldAppendNothingGivenInitialFactWhoseOperationIsAnotherCommandKind`, `ShouldAppendNothingGivenInitialFactCarryingAnExpectedRevision` |
| Duplicate record / no-op | `ShouldAppendNothingGivenSecondInitialReportOnRecordedIncident` |
| Replay of valid history | `ShouldRehydrateHeaderAndSequenceGivenRecordedAndRevisedHistory`, `ShouldReplayIncidentAssessmentAndResolutionGivenEvents` |
| Exact retry after later facts, changed intent | `ShouldReplayOriginalCreationGivenLaterCorrectionAndClockAdvance`, `ShouldAppendNothingGivenExactRetryOf*AfterTheClockMoved`, `ShouldConflictGivenChangedIntentUnderTheSameAssessCommandId` |
| Bounded history / envelope | `ShouldRetainCompleteChronologyGivenMoreThanThirtyTwoGovernedIncidentVersions`, `ShouldRejectOversizedConcreteEventGivenIndividuallyValidIncidentFields` |
| Assessment-once, resolution gates | `ShouldApplyAcceptedIncidentSignificanceRuleGivenAssessment`, `ShouldRequireReviewedScopeAndControlsBeforeIncidentResolutionGivenMaterialIncident`, resolution proof tests |

Malformed-history tests were not added, by user preference.

## Event contract status

The five facts carry header, content/change refs, sequence, operation provenance and result refs. No add, rename, remove or payload change requested. Catalog `baselineSourcePath` (`Features/Incidents/ComplianceIncident.cs`) is stale; the current path is `Features/Incidents/Aggregates/ComplianceIncident.cs` (tracker owner to update). The catalog invariant text omits the `detected_at`/`recorded_at` ordering rule, which is enforced in the rules type.

## Later-stage follow-ups (not aggregate gaps)

- Handlers own authorization, CommandId receipts, original-result binding and follow-up completion proof gathering.
- Reactor for the system-description decision; directory projector lag fencing.

## Evidence

- Jev: no request this round.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ComplianceIncident"`.
- RED: none captured; not claimed.
- Residual risk: `ComplianceIncidentOwnerTests` has not been compiled; the second-report test relies on the first Append guard (`Before` must be non-null once a header exists).

## Review before build

Independent review, written not executed (no build or test run).

Findings and fixes:

- Compile error fixed: `ComplianceIncidentOwnerTests` referenced `ComplianceIncidentRecorded` without `using Bdgrz.Compliance.Features.Incidents.Events;`. Added.
- Weak assertion removed: the second-report test kept a `rules` local only to assert `Assert.NotNull(rules.Current)`, which proves nothing about Append. Dropped; the test now awaits `SeedAsync()` directly and asserts the refusal, unchanged revision and sequence.
- Verified by reading source: every helper signature used (`ComplianceIncidentRules.Record`, `ComplianceIncidentContentStore.FreezeAsync/Fact`, `OwnerProvenance.For`, `NativeContent.FreezeUnitAsync`, `FirstResponses.FreezeAsync`, `OperationProvenance` with-members, `IncidentNativeFixture` members, handler constructor) matches. Realm strings equal `Tenant.ToString()`. Each refusal test traces to a distinct Append or Validate guard (program scope, stale sequence, command kind, expected revision on initial, Before-non-null on existing header).
- No implementation defect found: retries of committed facts are handled at the handler layer (existing tests); Append never emits a duplicate; no payload field duplicates metadata.

Jev (model jev-1.14.0, advisory), request and response in `implementation-jev/complianceincident-test-impl-review-2026-10-29-*.json`:

- Second initial report on a recorded incident: refuse_no_event 1.00. Consistent with code and test.
- Tests assert behavior and can fail (noul true): 0.93.
- Discrepancy, catalog omits detected_at/recorded_at ordering rule enforced in code: add_to_model 0.78 (needs_source_resolution 0.21, remove_from_code 0.01), confidence 0.67. Lead is clear but not overwhelming; code unchanged. Catalog edit is a shared file, reported to the tracker owner, with the stale `baselineSourcePath`.

Remaining risks: the new test file has never compiled; `CommittedStreamPosition` on a fresh aggregate is assumed 0 (position argument 0 in the valid-case test) and the rehydrate test depends on handler behavior.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
