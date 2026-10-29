# TypeIIPeriod aggregate review

## Identity, invariant and boundary

- **Identity:** tenant / `type-ii-periods` / PeriodId, with an immutable ProgramId bound by the opening. PeriodId identifies creation; the opening is the logical operation of the period (`RootOperationId == PeriodId`). Selected CommandId/GenerationKey identify later operations; no caller key forks the stream.
- **Named invariant:** one period binds the exact approved opening plan and expected population; close reconciles that population, acknowledges all unresolved material items independently, and freezes the same approved sources; close amendments chain from the original population and acknowledgements. Range changes are admitted by one pending root at a time, at the exact revision.
- **Exclusions:** the observation calendar (`TypeIIObservationCalendar`), plan approval (`TypeIIPlan`), control occurrences (`ControlOccurrence`, materialized by reactor from `DueControlOccurrencesGenerated`), full populations, source bodies and histories (reader composition), packages.
- **Decision:** keep as one aggregate. Opening, range amendments, generation, close validation, approval, freeze and close amendment share one revision and are decided atomically. Calendar permit coordination is explicit (pending root, `CalendarAuthorization`), never a projection check.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | `AcceptOpening` | `TypeIIPeriodOpeningAccepted` | root not the period, foreign affected/revision owner, unfrozen first response (Validation); second opening (Conflict) |
| opening pending | `RejectOpening` | `TypeIIPeriodOpeningRejected` | not the exact pending opening |
| opening pending | apply (Append) | `ObservationPeriodStarted` | prepared content or permit differs from authority |
| active | `AcceptAmendment` | `TypeIIPeriodAmendmentAccepted` | missing period, pending root, stale revision, envelope (Validation) |
| amendment pending | `CancelAmendment` / `RejectAmendment` | `TypeIIPeriodAmendmentCanceled` | not the exact pending amendment |
| amendment pending | apply (Append) | `ObservationPeriodAmended` | as above |
| active | generation | `DueControlOccurrencesGenerated` | outside period, repeated key with different intent |
| active/close_* | validation | `ObservationPeriodCloseProposed` | before period end, pending amendment |
| close_ready | approval | `TypeIIPeriodCloseApproved` | validator approving own validation |
| close_approved | freeze | `ObservationPeriodCloseFrozen` | other executive or missing live permit |
| closed | close amendment | `TypeIIPeriodCloseAmended` | lineage not advancing |

## Changes

- Gap: `AcceptOpening`/`AcceptAmendment` did not validate the frozen envelope (first-response schema, affected/expected-revision/first owner). A bad envelope raised an event whose own replay guard throws, leaving an unreplayable fact. Fixed with `ValidateAcceptanceEnvelope`, applied before any event is raised, mirroring the replay guard.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Invalid decision appends no event (envelope) | `ShouldAppendNothingGivenOpeningWithoutFrozenFirstResponse`, `ShouldAppendNothingGivenOpeningAffectingAnotherStream`, `ShouldAppendNothingGivenOpeningRootThatIsNotThePeriod` (TypeIIPeriodAcceptanceTests, new) |
| Competing pending root | `ShouldRefuseAmendmentGivenOpeningStillPending`, `ShouldRefuseSecondOpeningGivenOpeningAlreadyAcceptedOnPeriod` |
| Terminal fence / no reopen | `ShouldFenceOpeningForeverGivenCalendarRejection`, `ShouldRefuseDelayedApplyGivenAmendmentCanceledBeforeApply` |
| Wrong operation refused | `ShouldKeepOpeningPendingGivenRejectionNamingAnotherOperation` |
| Replay of valid history | `ShouldReplayOpeningAmendmentsCloseDecisionAndSnapshotHistoryGivenEvents`, handler hydration tests |
| Generation key reuse, close rules, freeze retry | existing `TypeIIPeriodTests`, `TypeIIPeriodCommandHandlerTests.*` |
| Bounded history | compact payload size check in `Append`; `ShouldOpenCompleteSourcePopulationGivenImmutableBodyExceedsEventBudget` |

## Event contract status

No event add, rename, remove or payload change needed. Payloads carry business facts and content references; metadata is not duplicated.

## Later-stage follow-ups

- Direct replay test of the compact aggregate over a cloned pending-event history (currently covered through handler hydration).
- Handler/reactor behavior (calendar apply, occurrence materialization) is outside this stage.

## Evidence

- Tests are written, not executed; build and run pending. RED was not observed. No dotnet command was run.

## Review before build

Independent review, nothing compiled or run.

Findings and fixes:
- `ShouldRefuseAmendmentGivenOpeningStillPending` was misnamed: with no applied header, `AcceptAmendment` refuses with missing-record before the pending-root check, so the pending conflict is unreachable in this test. Renamed to `ShouldRefuseAmendmentAndCancelGivenOpeningNotYetApplied`. The pending-root conflict on an applied period stays covered by the handler and fixture tests.
- All the new acceptance tests were refusals, so a bug refusing every opening would have passed them. Added `ShouldAppendOneOpeningFactGivenValidEnvelopeAndRefuseSecondOpening` (one fact, pending, program bound, second opening refused).
- Checked the other callers of the new envelope check: both handlers and `TypeIIPeriodSeeds` pass first responses frozen with `FirstResponses.FreezeAsync` and provenance with period-stream owners. `TypeIIPeriodTests` and `TypeIIPeriodTestExtensions` call `TypeIIPeriodRules`, not this aggregate, so their synthetic content is unaffected.
- No payload field duplicates metadata; no event change needed.

Jev (jev-1.14.0; request and response in `implementation-jev/typeiiperiod-test-impl-review-2026-10-29-*.json`):
- Envelope refusal: refuse_no_event 0.98 (decisive, applied).
- Amendment while opening pending: refuse 0.96 (consistent with the code).
- Fence test asserts the rule: Noul 0.83.
- Envelope refusal consistent with the rule: Noul 0.88.
No discrepancies with the catalog found.

Compile checks done by reading: `ActorReference.ForMember(Uuid, string)` exists; `OperationProvenance` has the 12-argument constructor used; `PendingChanges.Count` is used the same way in other tests; `FirstResponses.Schema` is public const; `ValidateAcceptanceEnvelope` matches the replay guard in `Accept`.

Remaining risks: not built or run; the direct replay test of the compact aggregate is still a follow-up; the pending-root conflict is not directly tested at aggregate level because it needs an applied header.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
