# ArtifactEngagementHold commands



Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/artifact-engagement-hold.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| PlaceArtifactEngagementHold | PlaceArtifactEngagementHold | creation | HoldId | 7 | [Placeholder: value not recorded] |
| RecordEngagementReportIssuance | RecordEngagementReportIssued | command | CommandId | 8 | [Placeholder: value not recorded] |
| ReleaseArtifactEngagementHold | ReleaseArtifactEngagementHold | command | CommandId | 6 | [Placeholder: value not recorded] |
| RecordEngagementPackageDelivery | RecordEngagementPackageDelivered | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### PlaceArtifactEngagementHold

| Selected contract detail | Value |
| --- | --- |
| Target command name | PlaceArtifactEngagementHold |
| Selected source command name | PlaceArtifactEngagementHold |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactEngagementHold |
| Selected source owner | ArtifactEngagementHold |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | HoldId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| HoldId | Uuid | true |
| EngagementId | Uuid | true |
| ReportArtifactId | Uuid? | true |
| Scope | string | true |
| Authority | string | true |
| Reason | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordEngagementReportIssuance

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordEngagementReportIssuance |
| Selected source command name | RecordEngagementReportIssued |
| Name decision | Records the exact auditor report and issue date that start the evidence preservation period. |
| Target owner | ArtifactEngagementHold |
| Selected source owner | ArtifactEngagementHold |
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
| HoldId | Uuid | true |
| EngagementId | Uuid | true |
| ReportArtifactId | Uuid | true |
| IssuedAt | DateTimeOffset | true |
| IssuerReference | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReleaseArtifactEngagementHold

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReleaseArtifactEngagementHold |
| Selected source command name | ReleaseArtifactEngagementHold |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactEngagementHold |
| Selected source owner | ArtifactEngagementHold |
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
| HoldId | Uuid | true |
| ExpectedRevision | long | true |
| ReleaseAuthority | string | true |
| Rationale | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordEngagementPackageDelivery

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordEngagementPackageDelivery |
| Selected source command name | RecordEngagementPackageDelivered |
| Name decision | Records delivery of the exact engagement package before its preservation hold can be released. |
| Target owner | ArtifactEngagementHold |
| Selected source owner | ArtifactEngagementHold |
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
| EngagementId | [Placeholder: value not recorded] | true |
| PackageId | [Placeholder: value not recorded] | true |
| DeliveryDecisionId | [Placeholder: value not recorded] | true |

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
