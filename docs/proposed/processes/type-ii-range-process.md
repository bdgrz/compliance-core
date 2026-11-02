# TypeIIRangeProcess

- Root source events: TypeIIPeriodOpeningAccepted, TypeIIPeriodAmendmentAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AcquireTypeIIPeriodRange | Old and new range held until applied. |
| apply | ApplyTypeIIPeriodRange | [Placeholder: no stage-specific rule recorded] |
| settle | SettleTypeIIPeriodRange | [Placeholder: no stage-specific rule recorded] |

Abort commands: RejectTypeIIPeriodOpening, RejectTypeIIPeriodAmendment, CancelTypeIIPeriodAmendment

Complete selected process definition: [JSON](../data/processes/type-ii-range-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
