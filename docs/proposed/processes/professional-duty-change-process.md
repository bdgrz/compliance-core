# ProfessionalDutyChangeProcess

- Root source events: ProfessionalDutyChangeAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AuthorizeFirmStaffProfessionalDutyUse | Recording only; revocation skips this stage. |
| claim | ClaimProfessionalAssertionIdentity | Permanent; never released. |
| apply | ApplyProfessionalDutyChange | [Placeholder: no stage-specific rule recorded] |
| settle | SettleFirmStaffProfessionalDutyUse | [Placeholder: no stage-specific rule recorded] |

Abort commands: RejectProfessionalDutyChange

Complete selected process definition: [JSON](../data/processes/professional-duty-change-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
