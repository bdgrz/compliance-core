# AuditorRequest aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One request (`AuditorRequest`, stream `auditor-requests/{requestId}` under the tenant realm) owns its terms, the admission seal and the resolution decision. Responses, comments and sample selections are admitted here and materialized as their own owners (`AuditorResponse`, `AuditorFeedbackThread`, `AuditorSampleSelection`); only a bounded count is retained here.

- Identity: tenant plus the caller-supplied immutable `RequestId` (exact immutable creation retry); the recording operation must be the request's own logical operation. Later mutations bind `CommandId` and the expected revision; the command id is acceptance bookkeeping and cannot fork the stream.
- Invariant: admission is sealed by an accepted resolution; a resolution is accepted once per generation, only over exactly the admitted responses, resolves only through the exact pending operation, and a reopen of a resolved request advances the generation.
- Exclusions: response review/delivery/disposition (`AuditorResponse`), sample content (`AuditorSampleSelection`), comment threads, authorization, content verification, resolution process progress (reactor).
- Splitting the seal from the admissions would let an admission race a resolution; the seal is decided on this stream, so no split.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record (own logical op) | recorded, revision 1, generation 1 | `AuditorRequestRecorded` |
| recorded | Record | state conflict | none |
| open | Revise / Assign (expected revision) | next revision | `AuditorRequestRevised` / `AuditorRequestAssigned` |
| open | Admit response / comment / selection | next revision, current generation | `AuditorResponseAdmitted` / `AuditorCommentAdmitted` / `AuditorSampleSelectionAdmitted` |
| open | Accept resolution (exact responses, at least one) | pending, admission sealed | `AuditorRequestResolutionAccepted` |
| pending | any write | transient conflict | none |
| pending | Complete (exact operation and intent) | resolved | `AuditorRequestResolved` |
| pending | Cancel (exact operation) | open again | `AuditorRequestResolutionCanceled` |
| resolved | any write | state conflict, reopen first | none |
| resolved | Reopen (expected revision) | open, generation + 1 | `AuditorRequestReopened` |
| open | Reopen | state conflict | none |
| any | stale expected revision | version conflict | none |

## Changes (real gaps)

Source: no change. The adversarial pass found the identity, seal, exact-set resolution, fence by pending operation, replay guards (tenant, subject, revision, generation, owner) and the replay cut already in place.

Coverage: the seal, resolved-state refusal and generation advance were only asserted indirectly. Added `AuditorRequestLifecycleTests` (new file, aggregate-only).

Observed, not changed: `ValidateResponses` requires a resolution to name every response admitted across all generations (the snapshot is whole-history), so a reopened request must name earlier-generation responses too. This matches "complete membership" and is not a defect; flagged for the owner if per-generation sets were intended.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, exact create retry, create conflict | `ShouldRecordExactRequestAndAllowOnlyExactCreateRetryGivenCreate` |
| Record / revise / assign / comment success | `AuditorRequestCommandHandlerTests` |
| Stale revision, no fact | `ShouldDiscardStaleRequestRevisionGivenPersonalCommand`, `ShouldRejectStaleHistoryAdmissionCertificateGivenLaterRequestFact` |
| Resolution without responses refused; reopen of open refused | `ShouldKeepRequestOpenGivenMissingResolutionPrerequisitesWhenResolveAndReopenCommands` |
| Exact response set, omitted response refused | `ShouldRejectStaleHistoryAdmissionCertificateGivenLaterRequestFact` |
| Pending resolution seals admission, second resolution refused | `ShouldSealAdmissionAndRefuseSecondResolutionGivenPendingResolution` (new) |
| Resolved refuses writes with no fact | `ShouldRefuseWritesGivenResolvedRequestWithNoFactAppended` (new) |
| Reopen advances generation, later admission in new generation | `ShouldAdmitCommentInNextGenerationGivenReopenedRequest` (new), `ShouldRetainRequestHistoryAcrossResolutionAndReopenGivenLifecycle` |
| Cancel fences delayed completion | `AuditorRequestUnboundCancellationTests`, `AuditorResolutionReplayTests` |
| Replay of valid history, replay cut | `AuditorRequestReplayCutTests`, `AuditorChildMaterializationReplayTests` |
| Competing appends, exact retry after later facts | `AuditorRequestPreparationRaceTests`, `AuditorRequestUnselectedHistoryTests`; framework OCC and native executor receipts |
| Bounded history | `ShouldKeepBoundedRequestAuthorityGivenManyAdmittedComments` |

Malformed-history tests were not added, by user preference.

## Event contract status

No event add, rename, remove or payload change requested. Admission facts carry intent and first-response references, not content; `Recorded`, `Revised`, `Assigned`, `Reopened` carry tenant, subject and revision alongside the stream address, a possible redundancy left to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: any pre-check ordering for pending versus resolved refusals stays in the command handlers.
- Resolution process, projections of request history and routes.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditorRequest"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file is uncompiled; the pending-state refusals assume the handlers surface `PrepareWrite`'s conflict kinds unchanged.

## Review before build

Independent review, written not executed; nothing compiled or run.

Findings and fixes:
- Source `AuditorRequest.cs` re-read against the lifecycle: seal (`PrepareWrite` pending is a transient Conflict, resolved is a non-transient state Conflict), exact-set resolution, reopen generation advance and replay guards all enforce the invariant. No source defect.
- Test weakness fixed: in `ShouldSealAdmissionAndRefuseSecondResolutionGivenPendingResolution` the second resolution was only asserted `IsSuccess == false`, which any failure (including validation) satisfies. It now asserts `Conflict` and `IsTransient`.
- Compile check by reading: usings (`Cntryl.Portia`, Handlers, Commands, `Processes.Processes`) match sibling tests; `Error!` after `Assert.False(IsSuccess)`; `Assert.Single`; no `.Count()`; command record signatures (`RecordAuditorRequestComment`, `ReviseAuditorRequest`, `ReopenAuditorRequest`) and fixture helpers (`DraftAsync`, `ResolveAsync`, `FinishResolutionAsync`, `RequestHistoryAsync`) confirmed by signature. Not confirmable without a build: that the executor surfaces the prepare error unchanged (existing tests rely on the same path).
- Note: the tests drive handlers through the fixture rather than the bare aggregate; this is the existing pattern for this owner because the seal is read from history by the handler path.

Discrepancies: none (no event or command missing from the catalog, no naming conflict, no payload field repeating stream identity). The whole-history response set is an owner-model observation, resolved by Jev below.

Jev (model jev-1.14.0), files `implementation-jev/auditorrequest-test-impl-review-2026-10-29-*.json`:
- Pending seal consistent with invariant: consistent 0.97.
- Pending transient vs resolved non-transient: right 1.00.
- Resolution names responses of all generations: whole_history 0.90 (per_generation 0.09); decisive, so the current behavior stands and the earlier open note is closed.
- Tests assert the invariant behaviorally: noul 0.59, low confidence, treated as unresolved; the tests assert outcomes (no revision change, generation 2) rather than only structure, but cover the aggregate through handlers.

Remaining risks: first compile and run of the new file; handler error mapping assumption above.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
