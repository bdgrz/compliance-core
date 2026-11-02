# AuditOutcome commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/audit-outcome.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| RecordExaminationOutcome | http | OutcomeId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/outcomes/{outcome_id} |

## Selected command details

### RecordExaminationOutcome

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditOutcome |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | OutcomeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExaminationId | Uuid | true |
| OutcomeId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | int | true |
| OutcomeCode | string | true |
| OutcomeSummary | string | true |
| IssuedReportReference | string | true |
| ReportArtifactId | Uuid? | true |
| IssuedOn | DateOnly | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/outcomes/{outcome_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
