# UserIdentity commands



Selected command contracts: **7**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/user-identity.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ContinueSignInWithExternalProvider | ContinueWithOidcProvider | http | none (no fields; provider and subject come from the authenticated OIDC principal); emits UserIdentityRegistered or UserIdentityAuthenticated, optionally UserIdentityProfileObserved | 0 | undefined undefined |
| SignInWithDevelopmentAccount | ContinueWithDeveloperIdentity | http | none (derived identity provider+subject) | 1 | undefined undefined |
| LinkExternalSignInToCurrentAccount | LinkOidcProviderIdentity | http | none (principal-derived) | 0 | undefined undefined |
| ProtectRecoveryIdentity | ProtectRecoveryIdentity | receiver | CommandId | 13 | [Placeholder: value not recorded] |
| EndRecoveryIdentityProtection | SettleRecoveryIdentityProtection | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| RegisterRecoveredIdentity | RegisterRecoveredIdentity | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| RevokeRecoveredOldIdentity | RevokeRecoveredOldIdentity | receiver | CommandId | 11 | [Placeholder: value not recorded] |

## Selected command details

### ContinueSignInWithExternalProvider

| Selected contract detail | Value |
| --- | --- |
| Target command name | ContinueSignInWithExternalProvider |
| Selected source command name | ContinueWithOidcProvider |
| Name decision | Completes sign-in with an identity verified by an external sign-in provider. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | none (no fields; provider and subject come from the authenticated OIDC principal); emits UserIdentityRegistered or UserIdentityAuthenticated, optionally UserIdentityProfileObserved; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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

[Placeholder: selected request fields are not recorded.]

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SignInWithDevelopmentAccount

| Selected contract detail | Value |
| --- | --- |
| Target command name | SignInWithDevelopmentAccount |
| Selected source command name | ContinueWithDeveloperIdentity |
| Name decision | Signs in with the deterministic development identity when developer sign-in is enabled. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | none (derived identity provider+subject); Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| EmailAddress | [Placeholder: value not recorded] | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### LinkExternalSignInToCurrentAccount

| Selected contract detail | Value |
| --- | --- |
| Target command name | LinkExternalSignInToCurrentAccount |
| Selected source command name | LinkOidcProviderIdentity |
| Name decision | Links the verified external sign-in identity to the account already signed in in this browser. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | none (principal-derived); Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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

[Placeholder: selected request fields are not recorded.]

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProtectRecoveryIdentity

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProtectRecoveryIdentity |
| Selected source command name | ProtectRecoveryIdentity |
| Name decision | The selected source name is retained. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
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
| ChallengeId | [Placeholder: value not recorded] | true |
| AccountId | [Placeholder: value not recorded] | true |
| OldIdentityId | [Placeholder: value not recorded] | true |
| ReplacementIdentityId | [Placeholder: value not recorded] | true |
| IdentityId | [Placeholder: value not recorded] | true |
| IdentityRole | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### EndRecoveryIdentityProtection

| Selected contract detail | Value |
| --- | --- |
| Target command name | EndRecoveryIdentityProtection |
| Selected source command name | SettleRecoveryIdentityProtection |
| Name decision | Ends protection of the exact recovery identity slot, including when protection never began. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
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
| ChallengeId | [Placeholder: value not recorded] | true |
| IdentityId | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RegisterRecoveredIdentity

| Selected contract detail | Value |
| --- | --- |
| Target command name | RegisterRecoveredIdentity |
| Selected source command name | RegisterRecoveredIdentity |
| Name decision | The selected source name is retained. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
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
| ChallengeId | [Placeholder: value not recorded] | true |
| AccountId | [Placeholder: value not recorded] | true |
| ReplacementIdentityId | [Placeholder: value not recorded] | true |
| ProtectionProofManifest | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RevokeRecoveredOldIdentity

| Selected contract detail | Value |
| --- | --- |
| Target command name | RevokeRecoveredOldIdentity |
| Selected source command name | RevokeRecoveredOldIdentity |
| Name decision | The selected source name is retained. |
| Target owner | UserIdentity |
| Selected source owner | UserIdentity |
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
| ChallengeId | [Placeholder: value not recorded] | true |
| OldIdentityId | [Placeholder: value not recorded] | true |
| ReplacementIdentityId | [Placeholder: value not recorded] | true |
| ReplacementRegistrationProof | [Placeholder: value not recorded] | true |

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
