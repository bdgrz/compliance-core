# TypeIIPeriod

- Classification: `business_aggregate`
- Source area: `Operations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / type-ii-periods / PeriodId, with immutable ProgramId and exact accepted plan/opening. PeriodId identifies creation; selected CommandId/GenerationKey identify exact freeze/reconciliation/amendment operations and their immutable closing identities.

**Invariant:** One period binds exact approved opening plan/population; close must reconcile the approved expected population, independently acknowledge all unresolved material items, and freeze the same approved sources. Close amendments preserve original expected population/acknowledgements and chain exact changed sources.

**Additional decision rule:** Period close lists every significant item in the period and fails while any flagged system description section remains unresolved.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AmendTypeIIPeriodClose](../commands/type-ii-period.md#amendtypeiiperiodclose) | command | CommandId |
| [ApproveTypeIIPeriodClose](../commands/type-ii-period.md#approvetypeiiperiodclose) | http | CommandId |
| [FreezeTypeIIPeriodClose](../commands/type-ii-period.md#freezetypeiiperiodclose) | http | CommandId |
| [OpenDueControlOccurrences](../commands/type-ii-period.md#openduecontroloccurrences) | creation | GenerationKey |
| [StartTypeIIObservationPeriod](../commands/type-ii-period.md#starttypeiiobservationperiod) | http | PeriodId |
| [AmendTypeIIObservationPeriod](../commands/type-ii-period.md#amendtypeiiobservationperiod) | http | CommandId |
| [CancelTypeIIPeriodAmendment](../commands/type-ii-period.md#canceltypeiiperiodamendment) | http | CommandId |
| [RejectTypeIIPeriodOpening](../commands/type-ii-period.md#rejecttypeiiperiodopening) | abort | CommandId |
| [RejectTypeIIPeriodAmendment](../commands/type-ii-period.md#rejecttypeiiperiodamendment) | abort | CommandId |
| [ApplyTypeIIPeriodRange](../commands/type-ii-period.md#applytypeiiperiodrange) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [DueControlOccurrencesGenerated](../events/type-ii-period.md#duecontroloccurrencesgenerated) | bdgrz.operations.due-control-occurrences-generated | 1 |
| [ObservationPeriodAmended](../events/type-ii-period.md#observationperiodamended) | bdgrz.operations.observation-period-amended | 1 |
| [ObservationPeriodCloseFrozen](../events/type-ii-period.md#observationperiodclosefrozen) | bdgrz.auditpopulations.observation-period-close-frozen | 1 |
| [ObservationPeriodCloseProposed](../events/type-ii-period.md#observationperiodcloseproposed) | bdgrz.auditpopulations.observation-period-close-proposed | 4 |
| [ObservationPeriodStarted](../events/type-ii-period.md#observationperiodstarted) | bdgrz.operations.observation-period-started | 1 |
| [TypeIIPeriodAmendmentAccepted](../events/type-ii-period.md#typeiiperiodamendmentaccepted) | bdgrz.type-ii-period.type-ii-period-amendment-accepted | 1 |
| [TypeIIPeriodAmendmentCanceled](../events/type-ii-period.md#typeiiperiodamendmentcanceled) | bdgrz.type-ii-period.type-ii-period-amendment-canceled | 1 |
| [TypeIIPeriodCloseAmended](../events/type-ii-period.md#typeiiperiodcloseamended) | bdgrz.auditpopulations.type-i-i-period-close-amended | 3 |
| [TypeIIPeriodCloseApproved](../events/type-ii-period.md#typeiiperiodcloseapproved) | bdgrz.auditpopulations.type-i-i-period-close-approved | 3 |
| [TypeIIPeriodOpeningAccepted](../events/type-ii-period.md#typeiiperiodopeningaccepted) | bdgrz.type-ii-period.type-ii-period-opening-accepted | 1 |
| [TypeIIPeriodOpeningRejected](../events/type-ii-period.md#typeiiperiodopeningrejected) | bdgrz.type-ii-period.type-ii-period-opening-rejected | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T040](../reactors/open-due-control-work-reactor.md#t040) | reaction_or_regular_process_stage | TypeIIPeriod | ControlOccurrence | OpenDueControlOccurrence |
| [T053](../reactors/reserve-accepted-type-ii-period-dates-reactor.md#t053) | reaction_or_regular_process_stage | TypeIIPeriod | TypeIIObservationCalendar | AcquireTypeIIPeriodRange |
| [T054](../reactors/reserve-accepted-type-ii-period-dates-reactor.md#t054) | reaction_or_regular_process_stage | TypeIIObservationCalendar | TypeIIPeriod | ApplyTypeIIPeriodRange |
| [T055](../reactors/reserve-accepted-type-ii-period-dates-reactor.md#t055) | reaction_or_regular_process_stage | TypeIIPeriod | TypeIIObservationCalendar | CloseTypeIIPeriodRangeReservation |
| [A018](../reactors/unassigned-domain-choreography.md#a018) | process_abort_binding | TypeIIPeriod | TypeIIPeriod | RejectTypeIIPeriodOpening |
| [A019](../reactors/unassigned-domain-choreography.md#a019) | process_abort_binding | TypeIIPeriod | TypeIIPeriod | RejectTypeIIPeriodAmendment |
| [A020](../reactors/unassigned-domain-choreography.md#a020) | process_abort_binding | TypeIIPeriod | TypeIIPeriod | CancelTypeIIPeriodAmendment |

## Product rules

- **D21-08** — Period close validation lists every significant item in the period and fails while any flagged system description section is unresolved. Decision: [m0-d21-significant-change.md](../decisions/m0-d21-significant-change.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/type-ii-period.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
