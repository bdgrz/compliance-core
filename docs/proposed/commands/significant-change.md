# SignificantChange commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/significant-change.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AssessSignificantChange | AssessSignificantChange | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/assessments/{command_id} |
| LinkImpactReviewWork | LinkImpactReviewWork | command | CommandId | 6 | [Placeholder: value not recorded] |
| RecordSignificantChange | RecordSignificantChange | creation | ChangeId | 4 | [Placeholder: value not recorded] |
| RecordSignificantChangeSystemDescriptionDecision | RecordSignificantChangeSystemDescriptionDecision | command | CommandId | 11 | [Placeholder: value not recorded] |
| ReviseSignificantChange | ReviseSignificantChange | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/revisions/{command_id} |

## Selected command details

### AssessSignificantChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | AssessSignificantChange |
| Selected source command name | AssessSignificantChange |
| Name decision | The selected source name is retained. |
| Owner | SignificantChange |
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
| ProgramId | Uuid | true |
| ChangeId | Uuid | true |
| ExpectedRevision | long | true |
| Content | SignificantChangeImpactContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/assessments/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### LinkImpactReviewWork

| Selected contract detail | Value |
| --- | --- |
| Target command name | LinkImpactReviewWork |
| Selected source command name | LinkImpactReviewWork |
| Name decision | The selected source name is retained. |
| Owner | SignificantChange |
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
| ChangeId | Uuid | true |
| ContentVersion | int | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordSignificantChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordSignificantChange |
| Selected source command name | RecordSignificantChange |
| Name decision | The selected source name is retained. |
| Owner | SignificantChange |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | ChangeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| ChangeId | Uuid | true |
| Content | SignificantChangeContent | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordSignificantChangeSystemDescriptionDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordSignificantChangeSystemDescriptionDecision |
| Selected source command name | RecordSignificantChangeSystemDescriptionDecision |
| Name decision | The selected source name is retained. |
| Owner | SignificantChange |
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
| ChangeId | Uuid | true |
| ContentVersion | int | true |
| SystemDescriptionId | Uuid | true |
| SystemDescriptionVersionId | Uuid | true |
| DecisionId | Uuid | true |
| Decision | string | true |
| AssessmentSha256 | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseSignificantChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseSignificantChange |
| Selected source command name | ReviseSignificantChange |
| Name decision | The selected source name is retained. |
| Owner | SignificantChange |
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
| ProgramId | Uuid | true |
| ChangeId | Uuid | true |
| ExpectedRevision | long | true |
| Content | SignificantChangeContent | true |
| ChangeReason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/revisions/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
