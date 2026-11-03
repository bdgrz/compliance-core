# IndependenceRuleRatificationCatalog commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/independence-rule-ratification-catalog.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RatifyIndependenceRuleVersion | RatifyIndependenceRuleVersion | http | CommandId | 8 | PUT /api/v1/platform/independence/rules/{rule_version}/ratification/{command_id} |
| AuthorizeIndependenceRuleRatificationCatalogEngagement | AuthorizeIndependenceRuleRatificationCatalogEngagementUse | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| CloseIndependenceRuleRatificationCatalogEngagementAuthorization | SettleIndependenceRuleRatificationCatalogEngagementUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### RatifyIndependenceRuleVersion

| Selected contract detail | Value |
| --- | --- |
| Target command name | RatifyIndependenceRuleVersion |
| Selected source command name | RatifyIndependenceRuleVersion |
| Name decision | The selected source name is retained. |
| Target owner | IndependenceRuleRatificationCatalog |
| Selected source owner | IndependenceRuleRatificationCatalog |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"IndependenceRuleRatificationView","firstHttpStatus":200,"terminalResultType":"IndependenceRuleRatificationView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| RatificationId | Uuid | true |
| RuleVersion | long | true |
| ExpectedRuleCatalogSequence | long | true |
| ExpectedRatificationSequence | long | true |
| ExpectedContentDigest | string | true |
| SourceReference | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/platform/independence/rules/{rule_version}/ratification/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeIndependenceRuleRatificationCatalogEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeIndependenceRuleRatificationCatalogEngagement |
| Selected source command name | AuthorizeIndependenceRuleRatificationCatalogEngagementUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Target owner | IndependenceRuleRatificationCatalog |
| Selected source owner | IndependenceRuleRatificationCatalog |
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
| Primary handler | AuthorizeIndependenceRuleRatificationCatalogEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AuthorizeIndependenceRuleRatificationCatalogEngagementUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| SourceContentEventId | Uuid | true |
| ExpectedContentRevision | ulong | true |
| EngagementId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CloseIndependenceRuleRatificationCatalogEngagementAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseIndependenceRuleRatificationCatalogEngagementAuthorization |
| Selected source command name | SettleIndependenceRuleRatificationCatalogEngagementUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Target owner | IndependenceRuleRatificationCatalog |
| Selected source owner | IndependenceRuleRatificationCatalog |
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
| Primary handler | SettleIndependenceRuleRatificationCatalogEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleIndependenceRuleRatificationCatalogEngagementUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| EngagementId | Uuid | true |
| Disposition | SourceUseSettlementDisposition | true |

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
