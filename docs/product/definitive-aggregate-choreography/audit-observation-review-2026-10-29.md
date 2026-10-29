# AuditObservation aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One stream, `audit-observations/{observationId}` under the tenant realm, owns one received auditor observation or exception: its received source versions and the latest management response and finding link, each of which must name the current received version.

- Identity: tenant plus the caller-supplied immutable `ObservationId` (exact creation retry returns the original result; changed intent under the same id is refused). `ProgramId` is immutable and checked inside the stream. Each later mutation is addressed by the exact nonempty selected `CommandId`; the command id is acceptance bookkeeping and cannot fork the stream. The aggregate retains only the selected command's fact for retry comparison.
- Invariant: received auditor source corrections preserve the examination and snapshot and the exact source lineage; response and finding-link commands must name the current source version; each command identity has immutable intent.
- Exclusions: source existence and recording authority (consuming handler), finding content and lifecycle (finding aggregate), imported-row provenance mapping (import process), authorization.
- Alternative considered: independent response/link owners. Rejected for now: a correction racing a response or link would lose the current-source-version gate without source-owner admission. Only the latest response and link are retained in memory, and events are bounded (32 KiB, 64 affected records); history growth is per correction/response/link and has no unbounded roster.
- Product rules: D27-03 (auditor wording preserved exactly as received; wording is not trimmed), D04-04 (consultant/imported content never attributed to a platform actor; actor is the recorder only), D04-01 (a missing source identifier is recorded by the caller as `source_identifier_absent`, never synthesized by the aggregate; blank is refused).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record (own id and command) | recorded, revision 1, source version 1 | `AuditObservationRecorded` |
| none | Correct / Respond / Link | missing record | none |
| recorded | Record, new command | state conflict | none |
| recorded | Record, same command, same intent | no-op (exact retry, also after later facts) | none |
| recorded | Record, same command, changed intent | state conflict | none |
| any | Correct (expected revision, current previous version, changed facts, same examination and snapshot) | source version + 1 | `AuditObservationCorrected` |
| any | Correct unchanged / rebinding / non-auditor | invalid content | none |
| any | Correct naming a non-current version | state conflict | none |
| any | Respond / Link naming current version | next revision, latest replaced | `AuditObservationManagementResponseRecorded` / `AuditObservationFindingLinked` |
| any | Respond / Link naming superseded or foreign version | invalid content | none |
| any | stale expected revision | version conflict | none |
| any | mutation without exact nonempty command selector | invalid content | none |

There is no terminal state in this slice; none is catalogued.

## Changes

Source: none. The adversarial pass found identity, immutable per-command intent (including retry after later facts), current-source-version gate, examination/snapshot preservation, replay guards (tenant, subject, program, revision, actor, digest, single selected fact) and event-size bound already in place. No real gap.

Coverage: added `AuditObservationLifecycleTests` (new aggregate-only file) for criteria with no direct test: stale revision on response and link, superseded source version, unchanged or non-current correction, missing record, second recording under a different command, source-identifier-absent marker and blank refusal, and a second response replacing the latest.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Verbatim wording, record success | `ShouldPreserveVerbatimAuditorWordingGivenReceivedException` |
| Lineage across correction and response | `ShouldPreserveSourceLineageGivenAuditorCorrectionAndManagementResponse` |
| Changed retry, stale correction | `ShouldRejectChangedRetryAndStaleCorrectionGivenRecordedSource` |
| Link scope and version | `ShouldRejectFindingLinkGivenWrongProgramOrSourceVersion` |
| Exact retry after later facts, changed response refused | `ShouldRememberExactOlderCommandGivenLaterCorrectionResponseAndFindingLink` |
| Command selector required | `ShouldRejectMutationGivenAbsentOrMismatchedCommandSelector`, `ShouldRejectRepeatedSelectedCommandIdentityGivenOtherwiseValidLaterFact` |
| Valid replay | `ShouldReplaySerializedFactsGivenCorrectedResponseAndFindingLink` |
| Examination/snapshot preserved, auditor authorship only | `ShouldRejectSourceRebindingOrUnsupportedAuthorshipGivenAuditorCorrection` |
| Bounded event, empty scope, identity | `ShouldRejectOversizedSerializedEventGivenOtherwiseBoundedSourceFields`, `ShouldRejectRecordingGivenEmptyAggregateScope`, `ShouldRejectConstructionGivenEmptyObservationIdentity` |
| Receipt immune to caller mutation | `ShouldPreserveEventAndSelectedReceiptGivenCallerMutatesSourceCollection` |
| Stale revision on response/link | `ShouldRefuseResponseAndFindingLinkGivenStaleExpectedRevision` (new) |
| Response names superseded version | `ShouldRefuseResponseGivenSupersededSourceVersion` (new) |
| Unchanged / non-current correction, no fact | `ShouldRefuseCorrectionGivenUnchangedContentOrNonCurrentPreviousVersion` (new) |
| Missing record | `ShouldReportMissingRecordGivenMutationBeforeRecording` (new) |
| Second recording, new command | `ShouldRefuseSecondRecordingGivenDifferentCommandOnExistingObservation` (new) |
| D04-01 marker and blank reference | `ShouldKeepAbsentSourceMarkerGivenImportedRowAndRefuseBlankReference` (new) |
| Repeat response replaces latest | `ShouldReplaceLatestResponseGivenSecondResponseOnCurrentSource` (new) |

Pre-existing malformed-history tests in `AuditObservationTests` are retained; none added, by user preference. Competing appends rely on the expected-revision check and Portia's append against the hydrated position.

## Event contract status

No event add, rename, remove or payload change requested. Four events (`Recorded`, `Corrected`, `ManagementResponseRecorded`, `FindingLinked`) match the catalog baseline. Payloads carry tenant, subject, revision and actor alongside the stream address, a possible metadata redundancy left to the schema owner (catalog `eventAuthority` requires schema reconciliation).

## Later-stage follow-ups (not aggregate gaps)

- Handlers and routes for the four planned commands (issue 301), including source existence, recording authority and mapping imported rows to the `source_identifier_absent` marker.
- Projection of source version history and latest response/link.
- Consultant-authored imported provenance (`imported_from`) mapping in the import process.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditObservation"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file is uncompiled.

## Review before build

Independent review, nothing compiled or executed.

Findings: no correctness gap in `AuditObservation.cs` (unchanged this session). Every accepted decision is gated on tenant, program, revision, current source version and the selected command identity; exact retries of committed facts return success with no new event. The seven new tests were walked through against the source: each fixture value is valid for the type that validates it, each asserts refusal codes plus an empty pending list or the replayed state, and none can pass while the refused fact is appended. Names follow CNTRYL0001 and every test has ordered Arrange/Act/Assert comments. Helper shapes (`Replay`, `PendingOf`, `Finding`, `Content`) mirror the existing `AuditObservationTests` helpers, and all usings match that file. Fixes applied: none needed.

Jev (model jev-1.14.0, advisory), request and response saved in `implementation-jev/auditobservation-test-impl-review-2026-10-29-*.json`:

| Question | Answer | Probability |
|---|---|---|
| Stale revision and superseded-version tests prove no fact appended | true | noul 0.94 |
| Unchanged and non-current correction tests prove refusals with no fact | true | noul 0.97 |
| Payload fields repeating stream identity (tenant, subject, revision, actor) | no_aggregate_change | 0.91 (confidence 0.88; remove_in_aggregate 0.08) |

Discrepancy disposition: repeated payload identity fields are decisive "leave"; reconciliation belongs to the schema owner (shared files), so it is listed under remaining, not applied. No catalog/code event name discrepancy.

Remaining risks: tests uncompiled and unexecuted; payload reconciliation pending with the schema owner.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
