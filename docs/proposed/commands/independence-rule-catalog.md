# IndependenceRuleCatalog commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/independence-rule-catalog.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ReviseIndependenceRules | ReviseIndependenceRules | command | CommandId | 3 | [Placeholder: value not recorded] |
| AuthorizeIndependenceRuleCatalogEngagement | AuthorizeIndependenceRuleCatalogEngagementUse | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| CloseIndependenceRuleCatalogEngagementAuthorization | SettleIndependenceRuleCatalogEngagementUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### ReviseIndependenceRules

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseIndependenceRules |
| Selected source command name | ReviseIndependenceRules |
| Name decision | The selected source name is retained. |
| Owner | IndependenceRuleCatalog |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"IndependenceRuleVersionView","firstHttpStatus":200,"terminalResultType":"IndependenceRuleVersionView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| ExpectedSequence | long | true |
| Content | IndependenceRuleContent | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeIndependenceRuleCatalogEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeIndependenceRuleCatalogEngagement |
| Selected source command name | AuthorizeIndependenceRuleCatalogEngagementUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | IndependenceRuleCatalog |
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
| Primary handler | AuthorizeIndependenceRuleCatalogEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AuthorizeIndependenceRuleCatalogEngagementUseSourceProof | true |
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

### CloseIndependenceRuleCatalogEngagementAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseIndependenceRuleCatalogEngagementAuthorization |
| Selected source command name | SettleIndependenceRuleCatalogEngagementUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | IndependenceRuleCatalog |
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
| Primary handler | SettleIndependenceRuleCatalogEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleIndependenceRuleCatalogEngagementUseSourceProof | true |
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
