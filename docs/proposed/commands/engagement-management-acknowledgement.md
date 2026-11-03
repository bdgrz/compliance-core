# EngagementManagementAcknowledgement commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/engagement-management-acknowledgement.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordEngagementManagementAcknowledgement | MaterializeEngagementManagementAcknowledgement | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| AcknowledgeEngagementManagement | AcknowledgeEngagementManagement | http | AcknowledgementId | 7 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### RecordEngagementManagementAcknowledgement

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordEngagementManagementAcknowledgement |
| Selected source command name | MaterializeEngagementManagementAcknowledgement |
| Name decision | Records the manager's acknowledgement of the exact engagement revision and service history. |
| Target owner | EngagementManagementAcknowledgement |
| Selected source owner | EngagementManagementAcknowledgement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; internal; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
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
| Intent | [Placeholder: value not recorded] | true |
| AcknowledgementId | [Placeholder: value not recorded] | true |
| SourceUseProofs | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AcknowledgeEngagementManagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcknowledgeEngagementManagement |
| Selected source command name | AcknowledgeEngagementManagement |
| Name decision | The selected source name is retained. |
| Target owner | EngagementManagementAcknowledgement |
| Selected source owner | EngagementManagementAcknowledgement |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | AcknowledgementId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"EngagementManagementAcknowledgementView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| AcknowledgementId | Uuid | true |
| ExpectedEngagementRevision | long | true |
| CompleteServiceRecordIds | IReadOnlyList<Uuid> | true |
| Statement | string | true |
| ExpectedServiceEpoch | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
