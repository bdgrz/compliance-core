# SystemInstanceAccessReviewScope commands



Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/system-instance-access-review-scope.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| DecideAccessReviewScope | DecideAccessReviewScope | http | CommandId | 12 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions/{command_id} |

## Selected command details

### DecideAccessReviewScope

| Selected contract detail | Value |
| --- | --- |
| Target command name | DecideAccessReviewScope |
| Selected source command name | DecideAccessReviewScope |
| Name decision | The selected source name is retained. |
| Target owner | SystemInstanceAccessReviewScope |
| Selected source owner | SystemInstanceAccessReviewScope |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessReviewScopeDecisionView","firstHttpStatus":200,"terminalResultType":"AccessReviewScopeDecisionView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedSystemInstanceRevision | long | true |
| ExpectedDecisionCount | long | true |
| Decision | string | true |
| Reason | string | true |
| EffectiveFrom | DateTimeOffset | true |
| ReviewBy | DateTimeOffset? | false |
| SeparationOfDutiesWaiverId | Uuid? | false |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
