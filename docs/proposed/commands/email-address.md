# EmailAddress commands

Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/email-address.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CompleteEmailChallenge | CompleteEmailChallenge | http | ChallengeId | 4 | PUT /api/v1/users/{user_id}/email-addresses/{email_address}/verifications/{challenge_id} |
| IssueEmailChallenge | IssueEmailChallenge | http | ChallengeId | 3 | PUT /api/v1/users/{user_id}/email-addresses/{email_address}/challenges/{challenge_id} |
| ReserveEmail | ReserveEmail | http | CommandId | 4 | PUT /api/v1/users/{user_id}/email-addresses/{email_address}/{command_id} |
| CompleteIdentityRecovery | CompleteIdentityRecovery | http | ChallengeId | 4 | {"method":"PUT","path":"/api/v1/identity-recovery/challenges/{challenge_id}/completion"} |
| StartIdentityRecovery | StartIdentityRecovery | http | ChallengeId | 2 | {"method":"PUT","path":"/api/v1/identity-recovery/challenges/{challenge_id}"} |
| CompleteEmailRecoveryChallenge | CompleteEmailRecoveryChallenge | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### CompleteEmailChallenge

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteEmailChallenge |
| Selected source command name | CompleteEmailChallenge |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ChallengeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| ChallengeId | Uuid | true |
| UserId | Uuid | true |
| EmailAddress | string | true |
| Token | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/users/{user_id}/email-addresses/{email_address}/verifications/{challenge_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### IssueEmailChallenge

| Selected contract detail | Value |
| --- | --- |
| Target command name | IssueEmailChallenge |
| Selected source command name | IssueEmailChallenge |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ChallengeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| ChallengeId | Uuid | true |
| UserId | Uuid | true |
| EmailAddress | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/users/{user_id}/email-addresses/{email_address}/challenges/{challenge_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReserveEmail

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReserveEmail |
| Selected source command name | ReserveEmail |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| UserId | Uuid | true |
| EmailAddress | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/users/{user_id}/email-addresses/{email_address}/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteIdentityRecovery

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteIdentityRecovery |
| Selected source command name | CompleteIdentityRecovery |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ChallengeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| Token | string | true |
| OldIdentityId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### StartIdentityRecovery

| Selected contract detail | Value |
| --- | --- |
| Target command name | StartIdentityRecovery |
| Selected source command name | StartIdentityRecovery |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ChallengeId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| ChallengeId | Uuid | true |
| EmailAddress | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteEmailRecoveryChallenge

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteEmailRecoveryChallenge |
| Selected source command name | CompleteEmailRecoveryChallenge |
| Name decision | The selected source name is retained. |
| Owner | EmailAddress |
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
| Primary handler | CompleteEmailRecoveryChallengeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<IdentityRecoveryCompleted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ChallengeId | Uuid | true |

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
