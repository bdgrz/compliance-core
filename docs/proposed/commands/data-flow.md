# DataFlow commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/data-flow.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordDataFlow | RecordDataFlow | http | DataFlowId | 14 | PUT /api/v1/tenants/{tenant_id}/data-flows/{data_flow_id} |
| ReviseDataFlow | ReviseDataFlow | command | CommandId | 17 | [Placeholder: value not recorded] |

## Selected command details

### RecordDataFlow

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordDataFlow |
| Selected source command name | RecordDataFlow |
| Name decision | The selected source name is retained. |
| Target owner | DataFlow |
| Selected source owner | DataFlow |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | DataFlowId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"DataFlowRegistration","firstHttpStatus":200,"terminalResultType":"DataFlowRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| DataFlowId | Uuid | true |
| TenantId | Uuid | true |
| SourceType | string | true |
| SourceId | Uuid | true |
| DestinationType | string | true |
| InformationAssetIds | IReadOnlyList<Uuid> | true |
| Purpose | string | true |
| EncryptedInTransit | bool | true |
| EncryptedAtRest | bool | true |
| EffectiveFrom | DateOnly | true |
| OwnerPersonId | Uuid | true |
| DestinationId | Uuid? | false |
| DestinationParty | string? | false |
| ExceptionReference | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/data-flows/{data_flow_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseDataFlow

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseDataFlow |
| Selected source command name | ReviseDataFlow |
| Name decision | The selected source name is retained. |
| Target owner | DataFlow |
| Selected source owner | DataFlow |
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
| DataFlowId | Uuid | true |
| ExpectedRevision | long | true |
| SourceType | string | true |
| SourceId | Uuid | true |
| DestinationType | string | true |
| InformationAssetIds | IReadOnlyList<Uuid> | true |
| Purpose | string | true |
| EncryptedInTransit | bool | true |
| EncryptedAtRest | bool | true |
| EffectiveFrom | DateOnly | true |
| OwnerPersonId | Uuid | true |
| Lifecycle | string | true |
| DestinationId | Uuid? | false |
| DestinationParty | string? | false |
| ExceptionReference | string? | false |

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
