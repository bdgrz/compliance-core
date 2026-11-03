# PolicyCampaignParticipant commands

Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/policy-campaign-participant.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AcknowledgePolicy | AcknowledgePolicy | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ApproveCampaignWaiver | ApproveCampaignWaiver | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordTrainingCompletion | RecordTrainingCompletion | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ApplyPolicyParticipantResult | ApplyPolicyParticipantResult | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| AuthorizePolicyCampaignParticipantCampaignClosure | AuthorizePolicyCampaignParticipantCampaignClosureUse | receiver | CommandId | 12 | [Placeholder: value not recorded] |
| ClosePolicyCampaignParticipantCampaignClosureAuthorization | SettlePolicyCampaignParticipantCampaignClosureUse | receiver | CommandId | 11 | [Placeholder: value not recorded] |

## Selected command details

### AcknowledgePolicy

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcknowledgePolicy |
| Selected source command name | AcknowledgePolicy |
| Name decision | The selected source name is retained. |
| Owner | PolicyCampaignParticipant |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignAcknowledgementView"}` |
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
| CampaignId | Uuid | true |
| PersonId | Uuid | true |
| PolicyVersion | long | true |
| ContentSha256 | string | true |
| AcknowledgementText | string | true |
| ExpectedCampaignRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApproveCampaignWaiver

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveCampaignWaiver |
| Selected source command name | ApproveCampaignWaiver |
| Name decision | The selected source name is retained. |
| Owner | PolicyCampaignParticipant |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignWaiverView"}` |
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
| CampaignId | Uuid | true |
| PersonId | Uuid | true |
| Reason | string | true |
| ExpiresOn | DateOnly | true |
| ExpectedCampaignRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordTrainingCompletion

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordTrainingCompletion |
| Selected source command name | RecordTrainingCompletion |
| Name decision | The selected source name is retained. |
| Owner | PolicyCampaignParticipant |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignCompletionView"}` |
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
| CampaignId | Uuid | true |
| PersonId | Uuid | true |
| RequirementVersion | long | true |
| CompletedOn | DateOnly | true |
| Source | string | true |
| EvidenceReference | string | true |
| ExpectedCampaignRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyPolicyParticipantResult

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApplyPolicyParticipantResult |
| Selected source command name | ApplyPolicyParticipantResult |
| Name decision | The selected source name is retained. |
| Owner | PolicyCampaignParticipant |
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
| CampaignId | [Placeholder: value not recorded] | true |
| PersonId | [Placeholder: value not recorded] | true |
| ResultKind | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizePolicyCampaignParticipantCampaignClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizePolicyCampaignParticipantCampaignClosure |
| Selected source command name | AuthorizePolicyCampaignParticipantCampaignClosureUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | PolicyCampaignParticipant |
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
| SourceOwnerId | [Placeholder: value not recorded] | true |
| SourceContentEventId | [Placeholder: value not recorded] | true |
| SourceRevision | [Placeholder: value not recorded] | true |
| ReceivingOwnerId | [Placeholder: value not recorded] | true |
| PersonId | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ClosePolicyCampaignParticipantCampaignClosureAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | ClosePolicyCampaignParticipantCampaignClosureAuthorization |
| Selected source command name | SettlePolicyCampaignParticipantCampaignClosureUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | PolicyCampaignParticipant |
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
| SourceOwnerId | [Placeholder: value not recorded] | true |
| ReceivingOwnerId | [Placeholder: value not recorded] | true |
| PersonId | [Placeholder: value not recorded] | true |
| Disposition | [Placeholder: value not recorded] | true |

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
