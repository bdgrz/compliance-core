# SignificantChange aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (narrow, per catalog).** One change (`SignificantChange`, stream `significant-changes/{ChangeId}` under the tenant) owns its current version, its single assessment per version, and the eligibility/independence gate (description decision plus required follow-up work links).

- Identity: caller-supplied immutable `ChangeId` is the creation identity and the stream key; selected command ids are acceptance bookkeeping and cannot fork the stream. `OwnerProvenance.RequireOwn` binds every fact to this stream.
- Invariant: one change version may be assessed once; the current-version independent description decision and required follow-up work gate advancement (period-close eligibility).
- Exclusions: authorization, source/provenance import, work and exception record lifecycle, system-description approval, projections and routes.
- Historical versions and decisions are immutable content (sealed content refs), not separate aggregates.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record (revision gate absent, own id) | revision 1, version 1 | `SignificantChangeRecorded` |
| recorded | Record | refusal | none |
| recorded | Revise (expected revision, changed content) | revision +1, version +1 | `SignificantChangeRevised` |
| recorded | Revise (identical content) | state conflict | none |
| version unassessed | Assess (expected revision) | revision +1 | `SignificantChangeImpactAssessed` |
| version assessed | Assess (changed intent) | state conflict | none |
| any | Assess (stale revision) | version conflict | none |
| none | Assess | not found | none |
| assessed, review required | Link review work | work linked | `ImpactReviewWorkLinked` |
| assessed, work linked | Description decision (independent actor) | eligible | `SignificantChangeSystemDescriptionDecisionRecorded` |
| assessed | Description decision by the assessor | actor prohibited | none |
| closed | Revise / Assess | state conflict | none |

## Changes (real gaps)

Source: none. `Append` already enforces the sequence/position gate, exact verified transition, fact kind to command binding, original expected revision gate for Revise and Assess, identity/program immutability, and bounded payload size; `SignificantChangeRules` enforces once-per-version assessment.

Tests: the pure rules suite lacked direct coverage of the named invariant. Added to `SignificantChangeTests`: second assessment, exact retry, stale revision, unrecorded change.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, own-stream binding | `SignificantChangeNativeTests` (record, owner stream on every fact) |
| Named invariant: assess once per version | `ShouldRefuseSecondAssessmentGivenVersionAlreadyAssessed` |
| Gate: independent decision and work links | `ShouldRequireExactReviewWorkAndSystemDescriptionDispositionGivenSignificantChange`, `ShouldRequireCorrectiveActionLinkGivenCurrentAssessmentWithoutDescriptionReview` |
| Invalid decision appends nothing | `ShouldRequireRuleBasedSignificanceAndAffectedRecordsGivenChangeImpactAssessment`, `ShouldRefuseAssessmentGivenChangeNotRecorded` |
| No-op / exact retry | `ShouldAcknowledgeExactRetryWithoutNewFactGivenIdenticalAssessmentRepeated`, `ShouldReplayOriginalAcceptanceGivenLaterRevisionAndLostResponseRetry` |
| Changed-intent refusal | same native test (changed content and actor refused) |
| Stale revision | `ShouldRefuseAssessmentGivenStaleExpectedRevision` |
| Replay of valid history | `ShouldReviseAndReplayChangeChronologyGivenLaterCorrection` |
| Competing appends | `ShouldRecoverEqualWinnerGivenImmutablePreparationFailsAfterConcurrentAcceptance` |
| Bounded history | `ShouldRetainCompleteChronologyGivenMoreThanThirtyTwoGovernedVersions`, `ShouldRecordCompleteGovernedReferencesGivenSourceContentExceedsEventBudget` |

## Event contract status

`SignificantChangeRecorded`, `...Revised`, `...ImpactAssessed`, `ImpactReviewWorkLinked`, `...SystemDescriptionDecisionRecorded`: no add, rename, remove or payload change requested. Facts carry header, content refs and operation binding; the operation binding is the native framework's, left to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Catalog notes the legacy `SameInitial`/`SameRevision`/`SameAssessment` helpers compare server clock values; native acceptance receipts now cover exact retry, so retiring those helpers is a cleanup.
- Handlers, projectors and routes for the change view and period-close eligibility.

## Evidence

- Jev: no request this round; decisions follow the catalog entry.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SignificantChange"`.
- RED: none captured; not claimed. The added tests pin existing behavior read from source.
- Residual risk: the new tests assume `Assess` raises revision 1 to 2 and `change.Current!.CurrentAssessment` is null before assessment.

## Review before build

Reviewer read the catalog entry, `SignificantChangeRules.Assess`, `SignificantChange.cs` and the four added tests. Nothing built or run.

Findings:
- Source: no defect found. `Assess` order is missing record, exact retry (`SameAssessment`: content digest, assessor, time), stale revision, closed, validation, once-per-version conflict. `SameAssessment` digests `SignificantChangeImpactContent` and the stored view with identical property names and order, so an exact retry matches.
- Tests: each asserts behavior (failure code, unchanged revision, unchanged `AppliedChanges` count, assessment state), not structure. The second-assessment fixture reuses a valid impact shape already used by existing tests, so it reaches the once-per-version branch rather than validation. The retry deliberately passes stale revision 1 to prove the retry is acknowledged before the revision gate (lost response).
- Compile read: `Assert.Null` in Arrange is fine; `AppliedChanges.Count` is a property on `IReadOnlyList<object>`; `Revision` is `long` and `Revision - 1` is `long`; names and phase comments follow CNTRYL0001/0002. No fixture ids are validated by a throwing factory.
- Nothing to fix. The earlier "Jev: no request this round" note is superseded by the section below.

Jev (model jev-1.14.0, request/response in `implementation-jev/significantchange-test-impl-review-2026-10-29-*.json`):

| Question | Answer | Probability |
|---|---|---|
| Second-assessment test proves refusal, no fact | noul true | 0.91 |
| Retry test proves acknowledged, no new fact | noul true | 0.88 |
| Stale-revision test proves refusal, no fact | noul true | 0.91 |
| Code events vs catalog discrepancy | no_discrepancy | 0.94 (needs_source_resolution 0.05, add_to_model 0.01, remove 0) |

Discrepancy check: catalog (`aggregates.json` baselineEvents and events) registers all five events, including `ImpactReviewWorkLinked`; code declares the same five. No event change needed.

Remaining risks: the tests are unexecuted; the `Assess` revision 1 to 2 assumption is confirmed from source (`Revision = Revision + 1`). The native `SignificantChange` aggregate (`Append`) has no new test this round; its coverage stays in `SignificantChangeNativeTests`.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
