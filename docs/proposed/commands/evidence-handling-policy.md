# EvidenceHandlingPolicy commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/evidence-handling-policy.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AssignArtifactHandlingPolicy | AssignArtifactHandlingPolicy | command | CommandId | 7 | [Placeholder: value not recorded] |
| AuthorizeArtifactHandlingDownload | AuthorizeArtifactHandlingDownloadUse | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| CloseArtifactHandlingDownloadAuthorization | SettleArtifactHandlingDownloadUse | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### AssignArtifactHandlingPolicy

| Selected contract detail | Value |
| --- | --- |
| Target command name | AssignArtifactHandlingPolicy |
| Selected source command name | AssignArtifactHandlingPolicy |
| Name decision | The selected source name is retained. |
| Target owner | EvidenceHandlingPolicy |
| Selected source owner | EvidenceHandlingPolicy |
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
| ArtifactId | Uuid | true |
| ExpectedRevision | long | true |
| HandlingClass | string | true |
| PolicyVersion | long | true |
| Rationale | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeArtifactHandlingDownload

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeArtifactHandlingDownload |
| Selected source command name | AuthorizeArtifactHandlingDownloadUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Target owner | EvidenceHandlingPolicy |
| Selected source owner | EvidenceHandlingPolicy |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | AuthorizeArtifactHandlingDownloadUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<EvidenceArtifactDownloadRequested> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ArtifactId | Uuid | true |
| PolicyVersion | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CloseArtifactHandlingDownloadAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseArtifactHandlingDownloadAuthorization |
| Selected source command name | SettleArtifactHandlingDownloadUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Target owner | EvidenceHandlingPolicy |
| Selected source owner | EvidenceHandlingPolicy |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | SettleArtifactHandlingDownloadUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleArtifactHandlingDownloadUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ArtifactId | Uuid | true |

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
