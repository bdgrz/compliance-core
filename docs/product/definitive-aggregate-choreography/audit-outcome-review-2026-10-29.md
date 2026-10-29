# AuditOutcome aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One stream, `audit-outcomes/{outcomeId}` under the tenant realm, owns one immutable auditor-supplied report conclusion.

- Identity: tenant plus caller-supplied immutable `OutcomeId` (exact retry returns the original result; changed intent under the same id is refused). `ProgramId` is immutable and checked on replay. No caller key forks the stream.
- Invariant: one auditor-supplied report conclusion is immutable for its outcome identity and exact delivered package/snapshot provenance (D27-01, D27-02).
- Exclusions: the report artifact lifecycle (own aggregate), management evaluations and sign-offs (separate records), package/snapshot existence and recording authority (consuming handler), authorization. The aggregate never computes an opinion or readiness.
- Alternative considered: split. Rejected; nothing inside the single accepted record can be mutated independently.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record valid auditor content, attributed recorder | recorded, revision 1 | `AuditOutcomeRecorded` |
| none | Record invalid content, missing recorder, empty program, blank display | invalid content | none |
| recorded | Record, same normalized content and recorder | no-op (later time and display ignored) | none |
| recorded | Record, changed content or different recorder | state conflict | none |

No terminal state; the record is immutable.

## Changes

Source: none. Identity, single immutable fact, normalized-content digest retry comparison, replay guards (tenant, subject, program, revision, actor, timestamps, digest) and event-size bound are in place. No real gap found.

Coverage: added `AuditOutcomeLifecycleTests` (new aggregate-only file) for criteria lacking a direct test: refusals append no fact, different-recorder retry, normalization-equivalent retry, and retry/changed intent after replay.

Observation, not changed: the aggregate trims summary and report reference and lowercases the outcome code and hashes; D27-02 says the outcome is retained "exactly as received". Edge whitespace is the only alteration; left as is pending a decision, not invented as a gap.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Record, auditor source, report binding | `ShouldRecordAuditorSuppliedOutcomeWithoutInferringProductReadinessGivenDeliveredPackage` |
| Product-calculated source refused | `ShouldRejectProductCalculatedOpinionGivenOutcomeContent` |
| Exact retry, later time | `ShouldTreatExactOutcomeRetryAsIdempotentGivenLaterRecordingTime` |
| Changed intent refused | `ShouldRejectChangedOutcomeRetryGivenRecordedOutcomeIdentity` |
| Valid replay | `ShouldReplayAuditorOutcomeAndExactReportBindingGivenRecordedOutcome` |
| Invalid decisions append nothing | `ShouldAppendNoFactGivenInvalidProvenanceOrArtifactBinding`, `ShouldAppendNoFactGivenMissingRecorderOrEmptyProgramScope` (new) |
| Different recorder refused | `ShouldRefuseRetryWithoutAppendingGivenDifferentRecorder` (new) |
| Normalized-equivalent retry no-op | `ShouldTreatNormalizedEquivalentRetryAsNoOpGivenHashCaseAndPaddingDifferences` (new) |
| Retry and changed intent after replay | `ShouldKeepImmutableOutcomeGivenRetryAndChangedIntentAfterReplay` (new) |

Stale revision and competing appends do not apply: one creation fact, no expected-revision command; Portia appends against the hydrated position. History is bounded at one fact.

## Event contract status

No event add, rename, remove or payload change requested. `AuditOutcomeRecorded` matches the catalog baseline. Payload repeats tenant, subject and revision alongside the stream address; reconciliation (catalog `eventAuthority`) belongs to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Handler and route for `RecordExaminationOutcome` (PUT outcomes/{outcome_id}): package, snapshot and examination existence, artifact existence, authority.
- Projection of outcomes per examination.

## Evidence

- Jev: no request this round.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditOutcome"`.
- RED: none captured; not claimed. Residual risk: new test file uncompiled.

## Review before build (2026-10-29)

Independent review of source and tests before any build. Evidence is written, not executed.

### Findings and fixes

- **Compile error (fixed):** `AuditOutcomeLifecycleTests` used `CommandFailure` and `CommandFailureCode` without `using Bdgrz.Compliance.Features.Versioning.Models;` (not a global using in the test project). Added.
- **CNTRYL0002 risk (fixed):** the invalid-content test placed Act and Assert comments inside a `foreach`. Restructured to Arrange, then an Act loop collecting outcomes and failures, then an Assert loop.
- **Coverage (added):** invalid case `ReportArtifactId = Uuid.Empty` (source refuses it).
- Source: no defect found. Retry compares the normalized-content digest and the canonical recorder; a different recorder or changed content yields StateConflict with no append; replay guards cover tenant, subject, program, revision, actor, timestamps and digest. Unverified compile risk: none known beyond the uncompiled new file.

### Jev (model jev-1.14.0; request and response in `implementation-jev/auditoutcome-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Invalid-content test proves nothing appended | true | 0.90 |
| Different-recorder retry | conflict | 1.00 |
| Normalized-equivalent retry test consistent with rule | true | 0.96 |
| D27-02 "exactly as received" vs edge trim/lower-case | keep_normalization | 0.86 (retain_exact 0.02, needs_source_resolution 0.12) |
| Premise: edge-trim does not alter the opinion | true | 0.95 |
| Null artifact id + null hash vs catalog `Uuid?` required | keep_nullable_pair | 0.77 (require_artifact 0.18) |
| Premise: required nullable means supply, null allowed | true | 0.72 |

### Discrepancy dispositions

1. Trimming vs "exactly as received": decisive (0.86). Keep normalization; no code change. Previously flagged open item is resolved as advisory keep.
2. `ReportArtifactId` nullable pair: decisive (0.77, premise 0.72). Keep the nullable pair; no code change. Premise is moderate; the catalog owner may confirm.

### Remaining risks

- Nothing built or run. Payload repeats tenant, subject and revision (schema owner). No event changes requested.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
