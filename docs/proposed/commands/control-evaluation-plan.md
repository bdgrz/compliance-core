# ControlEvaluationPlan commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-evaluation-plan.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| DefineControlEvaluationPlan | http | PlanVersionId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions/{plan_version_id} |

## Selected command details

### DefineControlEvaluationPlan

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluationPlan |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | PlanVersionId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationPlanVersionView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationPlanVersionView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| PlanVersionId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ControlVersionId | Uuid | true |
| ExpectedVersion | long | true |
| Objective | string | true |
| Steps | IReadOnlyList<EvaluationProcedureStep>? | true |
| TesterIndependenceRequired | bool | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions/{plan_version_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
