# AuditPackageBuildProcess

- Root source events: AuditPackageBuildRequested
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| start | StartAuditPackageBuild | Technical process; attempts are process state. |
| record | RecordAuditPackageBuildResult | Only a verified archive. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/audit-package-build-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
