# DeclaredSystemInstance commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/declared-system-instance.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| DeclareSystemInstance | DeclareSystemInstance | http | SystemInstanceId | 8 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id} |
| RetireSystemInstance | RetireSystemInstance | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements/{command_id} |
| ReviseSystemInstance | ReviseSystemInstance | command | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### DeclareSystemInstance

| Selected contract detail | Value |
| --- | --- |
| Target command name | DeclareSystemInstance |
| Selected source command name | DeclareSystemInstance |
| Name decision | The selected source name is retained. |
| Target owner | DeclaredSystemInstance |
| Selected source owner | DeclaredSystemInstance |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | SystemInstanceId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"SystemInstanceRegistration","firstHttpStatus":200,"terminalResultType":"SystemInstanceRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| SystemInstanceId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| ExpectedApplicationRevision | long | true |
| Name | string | true |
| Kind | string | true |
| AccessBoundaryReference | string? | false |
| SourceIdentifier | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RetireSystemInstance

| Selected contract detail | Value |
| --- | --- |
| Target command name | RetireSystemInstance |
| Selected source command name | RetireSystemInstance |
| Name decision | The selected source name is retained. |
| Target owner | DeclaredSystemInstance |
| Selected source owner | DeclaredSystemInstance |
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
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedRevision | long | true |
| EffectiveAt | DateTimeOffset | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseSystemInstance

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseSystemInstance |
| Selected source command name | ReviseSystemInstance |
| Name decision | The selected source name is retained. |
| Target owner | DeclaredSystemInstance |
| Selected source owner | DeclaredSystemInstance |
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
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| SourceIdentifier | string | true |
| AccessBoundary | string | true |
| Rationale | string | true |

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
