# AccessReviewDecisionBatch commands



Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-review-decision-batch.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CancelAccessReviewDecisionBatch | CancelAccessReviewDecisionBatch | abort | CommandId | 4 | [Placeholder: route/channel not recorded] |
| PublishAccessReviewDecisionBatch | PublishAccessReviewDecisionBatch | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| CompleteAccessReviewDecisionBatchCancellation | CompleteAccessReviewDecisionBatchCancellation | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| RecordAccessReviewDecisionBatch | MaterializeAccessReviewDecisionBatch | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### CancelAccessReviewDecisionBatch

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelAccessReviewDecisionBatch |
| Selected source command name | CancelAccessReviewDecisionBatch |
| Name decision | The selected source name is retained. |
| Target owner | AccessReviewDecisionBatch |
| Selected source owner | AccessReviewDecisionBatch |
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
| DecisionBatchId | Uuid | true |
| ExpectedRevision | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### PublishAccessReviewDecisionBatch

| Selected contract detail | Value |
| --- | --- |
| Target command name | PublishAccessReviewDecisionBatch |
| Selected source command name | PublishAccessReviewDecisionBatch |
| Name decision | The selected source name is retained. |
| Target owner | AccessReviewDecisionBatch |
| Selected source owner | AccessReviewDecisionBatch |
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
| Primary handler | PublishAccessReviewDecisionBatchHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| DecisionBatchId | Uuid | true |
| ItemDecisionProofs | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteAccessReviewDecisionBatchCancellation

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteAccessReviewDecisionBatchCancellation |
| Selected source command name | CompleteAccessReviewDecisionBatchCancellation |
| Name decision | The selected source name is retained. |
| Target owner | AccessReviewDecisionBatch |
| Selected source owner | AccessReviewDecisionBatch |
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
| Primary handler | CompleteAccessReviewDecisionBatchCancellationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchCancellationAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| DecisionBatchId | Uuid | true |
| ItemFenceProofs | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordAccessReviewDecisionBatch

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAccessReviewDecisionBatch |
| Selected source command name | MaterializeAccessReviewDecisionBatch |
| Name decision | Records the already admitted exact bulk selection without rebuilding or changing it. |
| Target owner | AccessReviewDecisionBatch |
| Selected source owner | AccessReviewDecisionBatch |
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
| Primary handler | MaterializeAccessReviewDecisionBatchHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| DecisionBatchId | Uuid | true |

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
