# TemplateApplication commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/template-application.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApplyMethodologyTemplate | ApplyMethodologyTemplate | creation | ApplicationKey | 5 | [Placeholder: value not recorded] |
| ProposeTemplateSuccessorAdoption | ProposeTemplateSuccessorAdoption | command | CommandId | 8 | [Placeholder: value not recorded] |
| DecideWhetherToAdoptNewTemplateVersion | RecordTemplateSuccessorDisposition | command | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### ApplyMethodologyTemplate

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApplyMethodologyTemplate |
| Selected source command name | ApplyMethodologyTemplate |
| Name decision | The selected source name is retained. |
| Target owner | TemplateApplication |
| Selected source owner | TemplateApplication |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | ApplicationKey; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| TemplateId | Uuid | true |
| TemplateVersion | long | true |
| ApplicationKey | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProposeTemplateSuccessorAdoption

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeTemplateSuccessorAdoption |
| Selected source command name | ProposeTemplateSuccessorAdoption |
| Name decision | The selected source name is retained. |
| Target owner | TemplateApplication |
| Selected source owner | TemplateApplication |
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
| TemplateId | Uuid | true |
| ApplicationKey | string | true |
| ExpectedRevision | long | true |
| NewTemplateVersion | long | true |
| Disposition | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DecideWhetherToAdoptNewTemplateVersion

| Selected contract detail | Value |
| --- | --- |
| Target command name | DecideWhetherToAdoptNewTemplateVersion |
| Selected source command name | RecordTemplateSuccessorDisposition |
| Name decision | Decides whether this template application will adopt the proposed new version, with a recorded rationale. |
| Target owner | TemplateApplication |
| Selected source owner | TemplateApplication |
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
| TemplateId | Uuid | true |
| ApplicationKey | string | true |
| ExpectedRevision | long | true |
| NewTemplateVersion | long | true |
| Disposition | string | true |
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
