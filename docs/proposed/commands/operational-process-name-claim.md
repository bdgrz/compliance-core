# OperationalProcessNameClaim commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/operational-process-name-claim.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ClaimOperationalProcessName | ClaimOperationalProcessName | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| ReleaseOperationalProcessName | ReleaseOperationalProcessName | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### ClaimOperationalProcessName

| Selected contract detail | Value |
| --- | --- |
| Target command name | ClaimOperationalProcessName |
| Selected source command name | ClaimOperationalProcessName |
| Name decision | The selected source name is retained. |
| Target owner | OperationalProcessNameClaim |
| Selected source owner | OperationalProcessNameClaim |
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
| RecordId | [Placeholder: value not recorded] | true |
| NormalizedName | [Placeholder: value not recorded] | true |
| NameKind | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReleaseOperationalProcessName

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReleaseOperationalProcessName |
| Selected source command name | ReleaseOperationalProcessName |
| Name decision | The selected source name is retained. |
| Target owner | OperationalProcessNameClaim |
| Selected source owner | OperationalProcessNameClaim |
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
| RecordId | [Placeholder: value not recorded] | true |
| NormalizedName | [Placeholder: value not recorded] | true |

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
