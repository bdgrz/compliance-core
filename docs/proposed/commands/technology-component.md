# TechnologyComponent commands

Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/technology-component.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordTechnologyComponent | RecordTechnologyComponent | http | ComponentId | 10 | PUT /api/v1/tenants/{tenant_id}/technology-components/{component_id} |
| ReviseTechnologyComponent | ReviseTechnologyComponent | command | CommandId | 11 | [Placeholder: value not recorded] |

## Selected command details

### RecordTechnologyComponent

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordTechnologyComponent |
| Selected source command name | RecordTechnologyComponent |
| Name decision | The selected source name is retained. |
| Owner | TechnologyComponent |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ComponentId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"TechnologyComponentRegistration","firstHttpStatus":200,"terminalResultType":"TechnologyComponentRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ComponentId | Uuid | true |
| TenantId | Uuid | true |
| Category | string | true |
| Name | string | true |
| OwnerPersonId | Uuid | true |
| EnvironmentReference | string? | false |
| LocationReference | string? | false |
| SystemInstanceId | Uuid? | false |
| EndpointCount | int? | false |
| ManagementSource | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/technology-components/{component_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseTechnologyComponent

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseTechnologyComponent |
| Selected source command name | ReviseTechnologyComponent |
| Name decision | The selected source name is retained. |
| Owner | TechnologyComponent |
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
| ComponentId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| OwnerPersonId | Uuid | true |
| Lifecycle | string | true |
| EnvironmentReference | string? | false |
| LocationReference | string? | false |
| EndpointCount | int? | false |
| ManagementSource | string? | false |

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
