# ImmutableSnapshot commands

Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/immutable-snapshot.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AmendProgramScopeSnapshot | AmendProgramScopeSnapshot | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments/{command_id} |
| FinalizeProgramScopeSnapshot | FreezeProgramScopeSnapshot | http | SnapshotId | 6 | PUT /api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id} |

## Selected command details

### AmendProgramScopeSnapshot

| Selected contract detail | Value |
| --- | --- |
| Target command name | AmendProgramScopeSnapshot |
| Selected source command name | AmendProgramScopeSnapshot |
| Name decision | The selected source name is retained. |
| Owner | ImmutableSnapshot |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"SnapshotRegistration","firstHttpStatus":200,"terminalResultType":"SnapshotRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| SnapshotId | Uuid | true |
| ProgramId | Uuid | true |
| ExpectedProgramRevision | long | true |
| BoundaryId | Uuid | true |
| ApprovedBoundaryVersionId | Uuid | true |
| Reason | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### FinalizeProgramScopeSnapshot

| Selected contract detail | Value |
| --- | --- |
| Target command name | FinalizeProgramScopeSnapshot |
| Selected source command name | FreezeProgramScopeSnapshot |
| Name decision | Finalizes the exact approved program scope as an immutable snapshot for later compliance work. |
| Owner | ImmutableSnapshot |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | SnapshotId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"SnapshotRegistration","firstHttpStatus":200,"terminalResultType":"SnapshotRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| SnapshotId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExpectedProgramRevision | long | true |
| BoundaryId | Uuid | true |
| ApprovedBoundaryVersionId | Uuid | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
