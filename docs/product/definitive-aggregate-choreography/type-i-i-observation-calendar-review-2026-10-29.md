# TypeIIObservationCalendar aggregate review

Reviewed 2026-10-29 against the catalog row `TypeIIObservationCalendar` (aggregates.json, polished 2026-10-29; boundary `narrow`). Scope is the aggregate stage only. Source: `src/Compliance.Core/Features/Operations/Aggregates/TypeIIObservationCalendar.cs`.

## Identity, invariant, exclusions

- **Identity:** stream `tenant/type-ii-observation-calendars/<ProgramId>`; `Id` is the program id. The optional selected logical id selects an acceptance only (`NativeAcceptance`); it never changes the stream. Each reservation is bound to its exact `PeriodId` and root operation id (the receiver command id derived from the admitted period fact).
- **Named invariant:** active Type II date ranges of one program, including current and reserved amendment ranges, never overlap; a pending amendment finishes or is canceled before the replaced range is released. Decided in `TypeIICalendarRules` over a prepared immutable before/after cut; the aggregate admits only the exact verified decision (`Append`) and re-checks counters, revision and generation on append and replay (`Validate`).
- **Exclusions:** period lifecycle and range intent (`TypeIIPeriod`), the acquire/settle protocol (`TypeIIRangeReactor`, `TypeIIRangeReleaseReactor`), and terminal history (immutable content, projection).

## Boundary decision

Keep together, narrow (matches catalog): splitting by period would break non-overlap without a shared admission owner. The aggregate keeps a compact `Header` and `CurrentAuthority` reference; live ranges and terminal history are verified immutable content, so command state is bounded. No split or reactor change.

## Lifecycle

| State | Decision | Result |
| --- | --- | --- |
| no live overlap | Opening acquire | `TypeIIPeriodRangeAuthorized` (Opening) |
| prior range live | Amendment acquire (new range free of other periods) | `TypeIIPeriodRangeAuthorized` (Amendment), old range held |
| overlap with another period | acquire | conflict, no fact (period is rejected by the reactor) |
| reservation live | Settle Activated (optionally releasing prior) | `TypeIIPeriodRangeSettled` |
| reservation live | Settle Released | `TypeIIPeriodRangeSettled`, live count -1 |
| no grant yet | Settle Fenced | `TypeIIPeriodRangeSettled`; late acquisition refused |
| settled or other period | settle | conflict, no fact |

## Changes this round

- No production change: identity, counters, selection, replay and cut checks already hold in source.
- Gap (coverage only): the existing `TypeIIObservationCalendarTests` exercise `TypeIICalendarRules`, not the aggregate. Process and native-history tests cover the aggregate through hosts but not `Append` directly. Added `TypeIIObservationCalendarAggregateTests` (new file).

## Test-to-criterion table

| Criterion | Status | Evidence |
| --- | --- | --- |
| Stable identity | covered | `ShouldKeepOneStreamPerProgramGivenAnySelectedLogicalId` |
| Named invariant (overlap, amendment hold, release, fence) | covered | `TypeIIObservationCalendarTests` (rules), `TypeIIRangeProcessTests` |
| Valid append | covered | `ShouldAppendOneAuthorizationGivenExactPreparedDecisionAndSelectedLogicalId` |
| Invalid decision appends nothing | covered | `ShouldAppendNothingGivenAnotherSelectedLogicalId`, `ShouldRefuseGivenFactThatDiffersFromPreparedDecision`, `ShouldRejectUnprovenAbsenceAtNativeAppendGivenTruncatedTerminalEvidence` |
| Exact retry, changed intent | covered (receiver/acceptance) | `TypeIIRangeProcessTests` (`ShouldReplayCancellationAndConflictGivenRetryWithChangedIntent`), `TypeIICalendarNativeHistoryTests` |
| Stale revision, competing appends | covered | `ShouldRefuseTransientlyGivenStaleNativeSequenceAfterCompetingAppend`, `ShouldGrantOneRangeAndConflictTheOtherGivenCompetingOverlappingStartsDeliveredConcurrently` |
| Replay of valid history | covered | `ShouldRebuildHeaderAuthorityAndAcceptanceGivenReplayOfAuthorization`, `ShouldRebuildReservationsFromOrderedFactsGivenReplay` |
| Bounded history | covered | `ShouldKeepCalendarOwnerBoundedGivenLifetimeReleasedReservations` |

## Event contract status

`TypeIIPeriodRangeAuthorized` and `TypeIIPeriodRangeSettled` (v2 discriminators `bdgrz.type-ii-observation-calendar...`) are unchanged. Payload carries range, period, kind and generation business facts plus native operation provenance; no event change requested.

## Later-stage follow-ups (not aggregate gaps)

- Catalog `baselineSourcePath` still names `Features/Operations/TypeIIObservationCalendar.cs`; refresh in the shared catalog.
- Backlog row 20 status update is a coordinator task.

## Evidence

Tests are written, not executed; build and run pending. Jev was not consulted: no open design decision or event-name change arose.

## Review before build

Independent review before any build. Nothing was compiled or executed.

### Findings and fixes

- Implementation: no defect found. Stale sequence or position is a transient conflict; a retry at the current sequence fails the before-cut header check (non-transient, no event); a tampered preparation fails `Verify`; a foreign selected logical id appends nothing; replay re-runs `Validate` on counters, revision and generation.
- Coverage gap fixed: a duplicate append of an already accepted authorization at the current sequence was untested (the stale-sequence test only covers the transient path). Added `ShouldAppendNoSecondFactGivenRetryOfAcceptedAuthorizationAtCurrentSequence`.
- Compile read: usings match the sibling `TypeIICalendarNativeHistoryTests`; `PendingEvents.Count` used as a property; replay seeded via `NativeFacts.Raised` and `Replay`; `ImmutableContentRef` is a record so `with` is valid; fixture ids are generated by the fixture. Unconfirmed: that `TypeIIRangeEffects.Child` sets `LogicalId` to the command id (assumed from the receivers, which hydrate with `command.CommandId`).
- Known coverage limit: settlement appends are covered only through host and process tests.

### Jev (model jev-1.14.0, advisory)

Request and response: `implementation-jev/typeiiobservationcalendar-test-impl-review-2026-10-29-{request,response}.json`.

| Question | Answer |
| --- | --- |
| Retry test proves no second fact | Noul 0.95 |
| Tamper test proves nothing appended | Noul 0.91 |
| Code events vs catalog discrepancy | no_discrepancy 1.00 |

No discrepancy; no event change needed.

### Remaining risks

Tests unexecuted; build and run pending. Catalog `baselineSourcePath` refresh and backlog row 20 stay with the coordinator.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
