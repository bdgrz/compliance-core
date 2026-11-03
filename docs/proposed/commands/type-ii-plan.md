# TypeIIPlan commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/type-ii-plan.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveTypeIIOperatingPlan | ApproveTypeIIOperatingPlan | command | CommandId | 7 | [Placeholder: value not recorded] |
| ProposeTypeIIOperatingPlan | ProposeTypeIIOperatingPlan | http | PlanId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-ii-plans/{plan_id}/proposals |
| ReviewTypeIIOperatingPlan | ReviewTypeIIOperatingPlan | command | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### ApproveTypeIIOperatingPlan

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveTypeIIOperatingPlan |
| Selected source command name | ApproveTypeIIOperatingPlan |
| Name decision | The selected source name is retained. |
| Owner | TypeIIPlan |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
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
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PlanId | Uuid | true |
| Version | long | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProposeTypeIIOperatingPlan

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeTypeIIOperatingPlan |
| Selected source command name | ProposeTypeIIOperatingPlan |
| Name decision | The selected source name is retained. |
| Owner | TypeIIPlan |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | PlanId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| PlanId | Uuid | true |
| ExaminationId | Uuid | true |
| Content | TypeIIPlanContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-ii-plans/{plan_id}/proposals | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviewTypeIIOperatingPlan

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewTypeIIOperatingPlan |
| Selected source command name | ReviewTypeIIOperatingPlan |
| Name decision | The selected source name is retained. |
| Owner | TypeIIPlan |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
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
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PlanId | Uuid | true |
| Version | long | true |
| Outcome | string | true |
| Comments | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
