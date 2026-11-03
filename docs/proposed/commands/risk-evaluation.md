# RiskEvaluation commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/risk-evaluation.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AcceptRisk | AcceptRisk | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances/{command_id} |
| ChooseRiskTreatment | ChooseRiskTreatment | command | CommandId | 7 | [Placeholder: value not recorded] |
| RecordRiskAssessment | RecordRiskAssessment | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments/{command_id} |

## Selected command details

### AcceptRisk

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcceptRisk |
| Selected source command name | AcceptRisk |
| Name decision | The selected source name is retained. |
| Target owner | RiskEvaluation |
| Selected source owner | RiskEvaluation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"RiskAcceptanceView","firstHttpStatus":200,"terminalResultType":"RiskAcceptanceView"}` |
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
| RiskId | Uuid | true |
| ExpectedRevision | long | true |
| ResidualAssessmentId | Uuid | true |
| ApproverAuthority | string | true |
| ExpiresAt | DateTimeOffset | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ChooseRiskTreatment

| Selected contract detail | Value |
| --- | --- |
| Target command name | ChooseRiskTreatment |
| Selected source command name | ChooseRiskTreatment |
| Name decision | The selected source name is retained. |
| Target owner | RiskEvaluation |
| Selected source owner | RiskEvaluation |
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
| RiskId | Uuid | true |
| ExpectedRevision | long | true |
| Kind | string | true |
| Rationale | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordRiskAssessment

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordRiskAssessment |
| Selected source command name | RecordRiskAssessment |
| Name decision | The selected source name is retained. |
| Target owner | RiskEvaluation |
| Selected source owner | RiskEvaluation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"RiskAssessmentView","firstHttpStatus":200,"terminalResultType":"RiskAssessmentView"}` |
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
| RiskId | Uuid | true |
| ExpectedRevision | long | true |
| MethodVersion | long | true |
| Phase | string | true |
| Likelihood | int | true |
| Impact | int | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
