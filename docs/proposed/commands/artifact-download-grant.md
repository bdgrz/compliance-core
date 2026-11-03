# ArtifactDownloadGrant commands

Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/artifact-download-grant.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CancelProtectedArtifactDownloadGrant | CancelProtectedArtifactDownloadGrant | abort | CommandId | 5 | [Placeholder: route/channel not recorded] |
| IssueProtectedArtifactDownloadGrant | IssueProtectedArtifactDownloadGrant | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### CancelProtectedArtifactDownloadGrant

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelProtectedArtifactDownloadGrant |
| Selected source command name | CancelProtectedArtifactDownloadGrant |
| Name decision | The selected source name is retained. |
| Owner | ArtifactDownloadGrant |
| Contract record status | [Placeholder: value not recorded] |
| Kind | abort |
| Logical identity | CommandId; Uuid; required=true; abort receiver identity bound to the exact admitted operation and generation: exact redelivery resolves the original rejected result; an abort after a terminal outcome is a no-op |
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
| CommandId | Uuid | true |
| GrantId | Uuid | true |
| RootOperationId | Uuid | true |
| Generation | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### IssueProtectedArtifactDownloadGrant

| Selected contract detail | Value |
| --- | --- |
| Target command name | IssueProtectedArtifactDownloadGrant |
| Selected source command name | IssueProtectedArtifactDownloadGrant |
| Name decision | The selected source name is retained. |
| Owner | ArtifactDownloadGrant |
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
| Primary handler | IssueProtectedArtifactDownloadGrantHandler |

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
| GrantId | Uuid | true |
| ArtifactId | Uuid | true |
| AuthorizationProofs | ImmutableContentRef | true |

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
