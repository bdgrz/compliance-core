# ManagementRepresentationLetter commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/management-representation-letter.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordManagementRepresentationLetter | RecordManagementRepresentationLetter | http | LetterId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/representation-letters/{letter_id} |
| SignManagementRepresentationLetter | SignManagementRepresentationLetter | command | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### RecordManagementRepresentationLetter

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordManagementRepresentationLetter |
| Selected source command name | RecordManagementRepresentationLetter |
| Name decision | The selected source name is retained. |
| Target owner | ManagementRepresentationLetter |
| Selected source owner | ManagementRepresentationLetter |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | LetterId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| ExaminationId | Uuid | true |
| LetterId | Uuid | true |
| AssertionId | Uuid | true |
| ArtifactId | Uuid | true |
| SourceReference | string | true |
| LetterDate | DateOnly | true |
| RequiredSignerIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/representation-letters/{letter_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SignManagementRepresentationLetter

| Selected contract detail | Value |
| --- | --- |
| Target command name | SignManagementRepresentationLetter |
| Selected source command name | SignManagementRepresentationLetter |
| Name decision | The selected source name is retained. |
| Target owner | ManagementRepresentationLetter |
| Selected source owner | ManagementRepresentationLetter |
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
| LetterId | Uuid | true |
| SignerMemberId | Uuid | true |
| SignedArtifactId | Uuid | true |
| SignedAt | DateTimeOffset | true |
| ExpectedRevision | long | true |

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
