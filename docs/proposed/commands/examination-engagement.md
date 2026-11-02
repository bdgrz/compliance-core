# ExaminationEngagement commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/examination-engagement.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| AmendTypeIBaseline | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/amendments/{command_id} |
| CreateExaminationEngagement | creation | ExaminationId | 10 | [Placeholder: value not recorded] |
| FreezeTypeIBaseline | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/freeze/{command_id} |
| ReviseExaminationEngagement | command | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### AmendTypeIBaseline

| Selected contract detail | Value |
| --- | --- |
| Owner | ExaminationEngagement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| ExaminationId | Uuid | true |
| BaselineId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |
| ChangedRecordIds | IReadOnlyList<Uuid> | true |
| ExpectedPreviewSha256 | string | true |
| AffectedPackageMaterialIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/amendments/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CreateExaminationEngagement

| Selected contract detail | Value |
| --- | --- |
| Owner | ExaminationEngagement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | ExaminationId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| ExaminationId | Uuid | true |
| EngagementType | string | true |
| AuditFirm | string | true |
| PeriodStart | DateOnly | true |
| PeriodEnd | DateOnly? | true |
| TrustServicesCategories | IReadOnlyList<string> | true |
| BoundaryId | Uuid | true |
| ManagementContactMemberIds | IReadOnlyList<Uuid>? | false |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### FreezeTypeIBaseline

| Selected contract detail | Value |
| --- | --- |
| Owner | ExaminationEngagement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| ExaminationId | Uuid | true |
| BaselineId | Uuid | true |
| ExpectedRevision | long | true |
| ConfirmationRationale | string | true |
| ExpectedPreviewSha256 | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/freeze/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseExaminationEngagement

| Selected contract detail | Value |
| --- | --- |
| Owner | ExaminationEngagement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| ExaminationId | Uuid | true |
| ExpectedRevision | long | true |
| AuditFirm | string | true |
| PeriodStart | DateOnly | true |
| PeriodEnd | DateOnly? | true |
| TrustServicesCategories | IReadOnlyList<string> | true |
| ManagementContactMemberIds | IReadOnlyList<Uuid>? | false |

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
