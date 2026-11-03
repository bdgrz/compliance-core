# WorkItemAssignment commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/work-item-assignment.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AssignWorkItem | AssignWorkItem | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments/{command_id} |
| ClaimWorkItem | ClaimWorkItem | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims/{command_id} |
| DelegateWorkItem | DelegateWorkItem | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations/{command_id} |
| EscalateWorkItem | EscalateWorkItem | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations/{command_id} |

## Selected command details

### AssignWorkItem

| Selected contract detail | Value |
| --- | --- |
| Target command name | AssignWorkItem |
| Selected source command name | AssignWorkItem |
| Name decision | The selected source name is retained. |
| Owner | WorkItemAssignment |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"WorkItemDetailView","firstHttpStatus":200,"terminalResultType":"WorkItemDetailView"}` |
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
| WorkItemId | Uuid | true |
| ExpectedRevision | long | true |
| AssigneeMemberId | Uuid | true |
| Reason | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ClaimWorkItem

| Selected contract detail | Value |
| --- | --- |
| Target command name | ClaimWorkItem |
| Selected source command name | ClaimWorkItem |
| Name decision | The selected source name is retained. |
| Owner | WorkItemAssignment |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"WorkItemDetailView","firstHttpStatus":200,"terminalResultType":"WorkItemDetailView"}` |
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
| WorkItemId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DelegateWorkItem

| Selected contract detail | Value |
| --- | --- |
| Target command name | DelegateWorkItem |
| Selected source command name | DelegateWorkItem |
| Name decision | The selected source name is retained. |
| Owner | WorkItemAssignment |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"WorkItemDetailView","firstHttpStatus":200,"terminalResultType":"WorkItemDetailView"}` |
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
| WorkItemId | Uuid | true |
| ExpectedRevision | long | true |
| AssigneeMemberId | Uuid | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### EscalateWorkItem

| Selected contract detail | Value |
| --- | --- |
| Target command name | EscalateWorkItem |
| Selected source command name | EscalateWorkItem |
| Name decision | The selected source name is retained. |
| Owner | WorkItemAssignment |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"WorkItemDetailView","firstHttpStatus":200,"terminalResultType":"WorkItemDetailView"}` |
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
| WorkItemId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
