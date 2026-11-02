# ApplicationImportExecutionProcess

- Root source events: ApplicationImportPlanSealed
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| claim | ClaimApplicationImportSource | [Placeholder: no stage-specific rule recorded] |
| reserve | ReserveApplicationImportTarget | Every target group in canonical order before any apply; driven by ApplicationImportExecutionAuthorized. |
| apply | ApplyApplicationImportEffect | One preflighted native batch per target group. |
| publish | CommitApplicationImportBatch | One publication gate. |
| settle | ReleaseApplicationImportTarget, ReleaseApplicationImportSource | [Placeholder: no stage-specific rule recorded] |

Abort commands: CancelApplicationImport

Complete selected process definition: [JSON](../data/processes/application-import-execution-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
