# WorkRelationship commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/work-relationship.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordWorkRelationship | RecordWorkRelationship | http | RelationshipId | 12 | PUT /api/v1/tenants/{tenant_id}/work-relationships/{relationship_id} |
| ReviseWorkRelationship | ReviseWorkRelationship | command | CommandId | 12 | [Placeholder: value not recorded] |

## Selected command details

### RecordWorkRelationship

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordWorkRelationship |
| Selected source command name | RecordWorkRelationship |
| Name decision | The selected source name is retained. |
| Target owner | WorkRelationship |
| Selected source owner | WorkRelationship |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | RelationshipId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"WorkRelationshipRegistration","firstHttpStatus":200,"terminalResultType":"WorkRelationshipRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| RelationshipId | Uuid | true |
| TenantId | Uuid | true |
| PersonId | Uuid | true |
| SourceWorkerId | string | true |
| WorkerType | string | true |
| LifecycleStatus | string | true |
| StartDate | DateOnly | true |
| EndDate | DateOnly? | false |
| Department | string? | false |
| ManagerPersonId | Uuid? | false |
| SponsorPersonId | Uuid? | false |
| EmploymentStatusReason | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/work-relationships/{relationship_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseWorkRelationship

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseWorkRelationship |
| Selected source command name | ReviseWorkRelationship |
| Name decision | The selected source name is retained. |
| Target owner | WorkRelationship |
| Selected source owner | WorkRelationship |
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
| RelationshipId | Uuid | true |
| ExpectedRevision | long | true |
| WorkerType | string | true |
| LifecycleStatus | string | true |
| StartDate | DateOnly | true |
| EndDate | DateOnly? | false |
| Department | string? | false |
| ManagerPersonId | Uuid? | false |
| SponsorPersonId | Uuid? | false |
| EmploymentStatusReason | string? | false |

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
