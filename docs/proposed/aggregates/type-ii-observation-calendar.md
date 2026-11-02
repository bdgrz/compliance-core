# TypeIIObservationCalendar

- Classification: `business_governor`
- Source area: `Operations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / type-ii-observation-calendars / ProgramId. Every active reservation remains bound to exact PeriodId/opening intent; release/cancel/close operations retain exact selected period, generation and terminal source proof.

**Invariant:** Active program Type II date ranges, including both current and reserved amendment ranges, must never overlap; a pending amendment must finish or cancel before date range release.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcquireTypeIIPeriodRange](../commands/type-ii-observation-calendar.md#acquiretypeiiperiodrange) | receiver | CommandId |
| [SettleTypeIIPeriodRange](../commands/type-ii-observation-calendar.md#settletypeiiperiodrange) | reactor | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TypeIIPeriodRangeAuthorized](../events/type-ii-observation-calendar.md#typeiiperiodrangeauthorized) | bdgrz.type-ii-observation-calendar.type-ii-period-range-authorized | 2 |
| [TypeIIPeriodRangeSettled](../events/type-ii-observation-calendar.md#typeiiperiodrangesettled) | bdgrz.type-ii-observation-calendar.type-ii-period-range-settled | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T053](../reactors/type-ii-range-reactor.md#t053) | reaction_or_regular_process_stage | TypeIIPeriod | TypeIIObservationCalendar | AcquireTypeIIPeriodRange |
| [T054](../reactors/type-ii-range-reactor.md#t054) | reaction_or_regular_process_stage | TypeIIObservationCalendar | TypeIIPeriod | ApplyTypeIIPeriodRange |
| [T055](../reactors/type-ii-range-reactor.md#t055) | reaction_or_regular_process_stage | TypeIIPeriod | TypeIIObservationCalendar | SettleTypeIIPeriodRange |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/type-ii-observation-calendar.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
