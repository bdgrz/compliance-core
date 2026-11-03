# ExaminationSignOff commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/examination-sign-off.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordManagementExaminationReview | RecordManagementExaminationReview | http | SignOffId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/management-reviews/{sign_off_id} |

## Selected command details

### RecordManagementExaminationReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordManagementExaminationReview |
| Selected source command name | RecordManagementExaminationReview |
| Name decision | The selected source name is retained. |
| Owner | ExaminationSignOff |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | SignOffId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
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
| SignOffId | Uuid | true |
| Content | ExaminationSignOffContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/management-reviews/{sign_off_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
