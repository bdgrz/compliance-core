# SystemBoundary commands



Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/system-boundary.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveBoundary | ApproveBoundary | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals/{command_id} |
| CreateBoundary | CreateBoundary | http | BoundaryId | 4 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries/{boundary_id} |
| DiscardBoundaryDraft | DiscardBoundaryDraft | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards/{command_id} |
| ProposeBoundarySuccessor | ProposeBoundarySuccessor | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors/{command_id} |
| ReviewBoundary | ReviewBoundary | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews/{command_id} |
| ReviseBoundaryDraft | ReviseBoundaryDraft | command | CommandId | 6 | [Placeholder: value not recorded] |

## Selected command details

### ApproveBoundary

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveBoundary |
| Selected source command name | ApproveBoundary |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
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
| BoundaryId | Uuid | true |
| DraftVersionId | Uuid | true |
| ExpectedRevision | long | true |
| AcceptedReviewDecisionId | Uuid | true |
| EffectiveFrom | DateOnly | true |
| Rationale | string | true |
| ImpactDigest | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CreateBoundary

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateBoundary |
| Selected source command name | CreateBoundary |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | BoundaryId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"BoundaryRegistration","firstHttpStatus":200,"terminalResultType":"BoundaryRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| BoundaryId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Content | BoundaryContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries/{boundary_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DiscardBoundaryDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | DiscardBoundaryDraft |
| Selected source command name | DiscardBoundaryDraft |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
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
| BoundaryId | Uuid | true |
| DraftVersionId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProposeBoundarySuccessor

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeBoundarySuccessor |
| Selected source command name | ProposeBoundarySuccessor |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"BoundaryRegistration","firstHttpStatus":200,"terminalResultType":"BoundaryRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BoundaryId | Uuid | true |
| ExpectedApprovedVersionId | Uuid | true |
| Content | BoundaryContent | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviewBoundary

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewBoundary |
| Selected source command name | ReviewBoundary |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
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
| BoundaryId | Uuid | true |
| DraftVersionId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseBoundaryDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseBoundaryDraft |
| Selected source command name | ReviseBoundaryDraft |
| Name decision | The selected source name is retained. |
| Target owner | SystemBoundary |
| Selected source owner | SystemBoundary |
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
| BoundaryId | Uuid | true |
| DraftVersionId | Uuid | true |
| ExpectedRevision | long | true |
| Content | BoundaryContent | true |

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
