# ArtifactDisposition commands



Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/artifact-disposition.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveArtifactDisposition | ApproveArtifactDisposition | command | CommandId | 6 | [Placeholder: value not recorded] |
| ExecuteArtifactDisposition | ExecuteArtifactDisposition | command | CommandId | 4 | [Placeholder: value not recorded] |
| RecordArtifactDispositionResult | RecordArtifactDispositionResult | command | CommandId | 10 | [Placeholder: value not recorded] |
| StopArtifactDisposalForHold | FenceArtifactDispositionForHold | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### ApproveArtifactDisposition

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveArtifactDisposition |
| Selected source command name | ApproveArtifactDisposition |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactDisposition |
| Selected source owner | ArtifactDisposition |
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
| DispositionId | Uuid | true |
| ArtifactId | Uuid | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ExecuteArtifactDisposition

| Selected contract detail | Value |
| --- | --- |
| Target command name | ExecuteArtifactDisposition |
| Selected source command name | ExecuteArtifactDisposition |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactDisposition |
| Selected source owner | ArtifactDisposition |
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
| DispositionId | Uuid | true |
| ArtifactId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordArtifactDispositionResult

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordArtifactDispositionResult |
| Selected source command name | RecordArtifactDispositionResult |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactDisposition |
| Selected source owner | ArtifactDisposition |
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
| DispositionId | Uuid | true |
| ArtifactId | Uuid | true |
| ExpectedRevision | long | true |
| AttemptNumber | int | true |
| ExecutionRequestEventId | Uuid | true |
| Outcome | string | true |
| FailureCode | string? | true |
| CompletedAt | DateTimeOffset | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### StopArtifactDisposalForHold

| Selected contract detail | Value |
| --- | --- |
| Target command name | StopArtifactDisposalForHold |
| Selected source command name | FenceArtifactDispositionForHold |
| Name decision | Stops an approved disposal when a new legal or engagement hold applies before deletion finishes. |
| Target owner | ArtifactDisposition |
| Selected source owner | ArtifactDisposition |
| Contract record status | PLANNED |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
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
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | [Placeholder: value not recorded] | true |
| Source | [Placeholder: value not recorded] | true |
| Target | [Placeholder: value not recorded] | true |
| OperationId | [Placeholder: value not recorded] | true |
| Generation | [Placeholder: value not recorded] | true |
| ExpectedRevision | [Placeholder: value not recorded] | true |
| HoldId | [Placeholder: value not recorded] | true |
| ArtifactContentId | [Placeholder: value not recorded] | true |

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
