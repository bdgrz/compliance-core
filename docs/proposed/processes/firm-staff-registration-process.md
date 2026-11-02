# FirmStaffRegistrationProcess

- Root source events: FirmStaffRegistrationAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| claim | ClaimFirmStaffIdentity | Permanent canonical-user claim. |
| complete | CompleteFirmStaffRegistration | [Placeholder: no stage-specific rule recorded] |

Abort commands: RejectFirmStaffRegistration

Complete selected process definition: [JSON](../data/processes/firm-staff-registration-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
