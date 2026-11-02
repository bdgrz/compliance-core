# ReadinessAssessment aggregate review (2026-10-29)

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched.

## Boundary decision

**Keep together; single immutable fact.** One stream per `(tenant, program, assessment)` identity (`AggregateIdentity.Create("ReadinessAssessment", tenantId, programId, assessmentId)`), area `readiness-assessments`. The assessment id is minted by the admitting `ProgramReadiness` run, never by a caller key, so no request key forks a stream.

Named invariant: one immutable assessment (rule version, as-of time, frozen inputs, findings, gaps, access-review scopes) is computed at most once, at the assessment's own revision one, from the frozen run intent.

Exclusions: latest-assessment pointer, adoption generation and decisions belong to `ProgramReadiness` (decides adoption and entry). Rule definitions and input selection belong to R1-08 and the run process; the assessment never selects inputs or reads live sources. Adoption of the assessment is reactor work (`ReadinessAssessmentAdoptionReactor`).

History is bounded: exactly one fact per stream; state holds only the recorded fact.

## Lifecycle

| State | Command | Result |
|---|---|---|
| not computed | `Record` with operation owned by this stream | `ReadinessAssessmentRecorded` at revision 1 |
| not computed | `Record` with operation owned by another stream | validation refusal, no fact |
| computed | `Record` / `PrepareRecord` by any other operation | conflict, no fact |
| computed | same logical id, same intent (handler, via `NativeAcceptance`) | replay of the original fact |
| computed | same logical id, changed intent | conflict (`NativeAcceptanceDecision.Conflict`) |

## Changes

No production change. Qualification found no behavioral gap: identity is derived from tenant, program and assessment; the reducer replays the same guards (tenant, program, assessment, revision one, own-stream operation, single record); exact retry and changed intent are decided by `NativeAcceptance` and the handler's `Decide`, which also covers retry after later facts since the stream has only one fact.

Tests added (aggregate only) in `ReadinessAssessmentContractTests.cs`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveStableIdentityGivenTenantProgramAndAssessment` (new) |
| Success appends one revision-one fact | `ShouldAppendOneRevisionOneFactGivenFirstComputation` (new) |
| Once-only, invalid decision appends nothing | `ShouldRefuseSecondComputationGivenAssessmentAlreadyRecorded`, `ShouldAppendNothingGivenOperationOwnedByAnotherAssessment` (new) |
| Valid replay, exact retry, changed intent | `ShouldRestoreRecordedAssessmentAndAcceptExactRetryGivenValidHistoryReplay` (new); existing `ShouldRetainCompleteScopeManifestGivenNativeFactRoundTripAndReplay` |
| Competing decisions | single-fact stream: second append refused by `PrepareRecord`; handler retries on `EventStreamConcurrencyException` and re-decides (existing handler tests) |
| Bounded history | one fact per stream |
| Wire shape | `ShouldSerializeAssessmentScopeSnapshotUsingSnakeCaseGivenDetailResponse` (existing) |

Malformed-history tests were not added, by user preference.

## Event contract status

No event changes needed. `ReadinessAssessmentRecorded` (`bdgrz.readiness.assessment.recorded` v1) carries business facts (rule version, as-of, edition, fingerprint, manifests, runner, intent) plus native operation provenance. Tenant, program, assessment id and revision repeat stream identity as business facts for readers; the reconciled catalog keeps them. `FirstResponse` and `OperationResult` are nullable native-operation slots left null by this receiver.

## Later-stage follow-ups (not aggregate gaps)

- `ReadinessAssessmentComputationReactor` (ProgramReadiness to ReadinessAssessment) and `ReadinessAssessmentAdoptionReactor` (ReadinessAssessment to ProgramReadiness).
- `Record` does not itself consult acceptance; the handler does, matching other native owners.

## Evidence

Tests are written, not executed; build and run pending. No Jev request was made for this slice. Helpers used (`ReadinessContent.Provenance`, `DomainEventSeed.Attach`, `AggregateScenario`, `AcceptedOperationBinding.Of`) were read but not compiled.

## Review before build

Independent review of source, tests and this file, read from the working tree (not the implementer summary). No build or test was run.

### Findings

- Implementation: no correctness gap. `Record` refuses a second computation (Conflict) and a foreign-stream operation (Validation) before raising; `Apply` repeats tenant, program, assessment, revision-one, own-stream and single-record guards. No duplicate or stale input can append a second fact. Exact retry is decided by `NativeAcceptance` in the handler, so no event is appended for it.
- Tests: each new test asserts events appended or not and state after replay; none is structural only. Names follow CNTRYL0001 and bodies carry ordered Arrange/Act/Assert comments. Fixture values are fresh Uuids and 64-character digests, which the types accept.
- Compile reading: `PendingEvents.Count` and `Assert.Single(PendingEvents)` match sibling tests; `Revision` is `long`, so the `1L` tuple compare is type-consistent; `OperationProvenance with { AuthoredIntentSha256 }`, `AcceptedOperationBinding.Of`, `NativeAcceptance.Decide` and `ReadinessContent.Provenance` signatures confirmed against source. `Result.Error?.Kind` is used the same way in sibling tests. Not confirmed by compiling.
- Fixes applied: none needed.

### Jev (model jev-1.14.0, advisory)

Request and response: `implementation-jev/readinessassessment-test-impl-review-2026-10-29-request.json` and `-response.json`. A first attempt returned HTTP 422 (schema error in my payload) and was resent corrected; only the successful response is kept.

| Question | Answer |
|---|---|
| Tests cover once-only, revision one, foreign operation, retry, changed intent | noul 0.93 |
| Second Record by another operation is Conflict with no fact | noul 0.97 |
| Payload repeats TenantId, ProgramId, AssessmentId, Revision (discrepancy) | keep_per_catalog 0.98, remove 0.01, needs_source_resolution 0.01 |
| Premise: catalog retains those fields | noul 0.93 |
| Rule version not checked in aggregate (discrepancy) | keep_in_run_process 0.99, add_aggregate_guard 0.01 |

Dispositions: identity fields kept, rule-version check stays in the run process; both decisive, no code change. The catalog premise for the identity fields came from the implementer's reading and `events.md` does not list payload fields, so it was not independently confirmed.

### Remaining risks

- Tests are written, not executed; compile unconfirmed.
- No competing-append test at aggregate level (single-fact stream; concurrency is handler territory).
- Tracker row in `aggregate-backlog.md` untouched (shared file).


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
