# AttestEngagementRecordPackage commands

In the selected source catalog these commands are grouped under `ClientExport`; the proposed target assigns them to `AttestEngagementRecordPackage` based on the selected business rule.

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/attest-engagement-record-package.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordAttestEngagementRecordPackageDelivery | RecordOwnAttestRecordDelivery | command | CommandId | 8 | [Placeholder: value not recorded] |
| RecordAttestEngagementPackageBuildResult | RecordOwnAttestRecordPackageResult | command | CommandId | 8 | [Placeholder: value not recorded] |
| RequestAttestEngagementRecordPackage | RequestOwnAttestRecordPackage | creation | PackageId | 4 | [Placeholder: value not recorded] |

## Selected command details

### RecordAttestEngagementRecordPackageDelivery

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAttestEngagementRecordPackageDelivery |
| Selected source command name | RecordOwnAttestRecordDelivery |
| Name decision | Records delivery of the exact attest engagement record package to the firm’s audit software. |
| Target owner | AttestEngagementRecordPackage |
| Selected source owner | ClientExport |
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
| EngagementId | Uuid | true |
| PackageId | Uuid | true |
| Recipient | string | true |
| DeliveredAt | DateTimeOffset | true |
| DeliveryReference | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordAttestEngagementPackageBuildResult

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAttestEngagementPackageBuildResult |
| Selected source command name | RecordOwnAttestRecordPackageResult |
| Name decision | Records whether generation of the firm’s attest engagement record package succeeded, including the exact artifact or failure. |
| Target owner | AttestEngagementRecordPackage |
| Selected source owner | ClientExport |
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
| PackageId | Uuid | true |
| Outcome | string | true |
| ArtifactId | Uuid? | true |
| FailureCode | string? | true |
| CompletedAt | DateTimeOffset | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RequestAttestEngagementRecordPackage

| Selected contract detail | Value |
| --- | --- |
| Target command name | RequestAttestEngagementRecordPackage |
| Selected source command name | RequestOwnAttestRecordPackage |
| Name decision | Requests the engagement record package required to preserve the firm’s own attest engagement. |
| Target owner | AttestEngagementRecordPackage |
| Selected source owner | ClientExport |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | PackageId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| EngagementId | Uuid | true |
| PackageId | Uuid | true |
| PackageKey | string | true |

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
