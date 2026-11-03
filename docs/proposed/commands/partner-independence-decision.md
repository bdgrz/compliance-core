# PartnerIndependenceDecision commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/partner-independence-decision.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordPartnerIndependenceEvaluation | MaterializePartnerIndependenceDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| SubmitPartnerIndependenceEvaluation | RecordPartnerIndependenceEvaluation | http | EvaluationId | 9 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/partner-evaluations/{evaluation_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### RecordPartnerIndependenceEvaluation

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordPartnerIndependenceEvaluation |
| Selected source command name | MaterializePartnerIndependenceDecision |
| Name decision | Records the partner evaluation against the exact engagement revision and service history. |
| Target owner | PartnerIndependenceDecision |
| Selected source owner | PartnerIndependenceDecision |
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
| EvaluationId | [Placeholder: value not recorded] | true |
| SourceUseProofs | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SubmitPartnerIndependenceEvaluation

| Selected contract detail | Value |
| --- | --- |
| Target command name | SubmitPartnerIndependenceEvaluation |
| Selected source command name | RecordPartnerIndependenceEvaluation |
| Name decision | Names the partner's action to submit an independence evaluation with its exact engagement, service history, and rule version. |
| Target owner | PartnerIndependenceDecision |
| Selected source owner | PartnerIndependenceDecision |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | EvaluationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"PartnerIndependenceEvaluationView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| EvaluationId | Uuid | true |
| ExpectedEngagementRevision | long | true |
| RuleVersion | long | true |
| ExpectedRuleContentDigest | string | true |
| ExpectedServiceHistoryDigest | string | true |
| Rationale | string | true |
| ExpectedServiceEpoch | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/partner-evaluations/{evaluation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
