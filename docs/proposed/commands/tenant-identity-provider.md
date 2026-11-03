# TenantIdentityProvider commands



Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/tenant-identity-provider.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ConfigureTenantSignInConnection | ConfigureTenantIdentityProvider | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/{command_id} |
| DisableTenantSignInConnection | DisableTenantIdentityProvider | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/disable/{command_id} |
| RecordSignInConnectionTestResult | RecordTenantIdentityProviderTestResult | command | CommandId | 7 | [Placeholder: value not recorded] |
| RotateTenantSignInCredentials | RotateTenantIdentityProviderCredentials | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/credential-rotations/{command_id} |
| TestTenantSignInConnection | TestTenantIdentityProvider | http | TestKey | 4 | PUT /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/tests/{test_key} |

## Selected command details

### ConfigureTenantSignInConnection

| Selected contract detail | Value |
| --- | --- |
| Target command name | ConfigureTenantSignInConnection |
| Selected source command name | ConfigureTenantIdentityProvider |
| Name decision | Sets up the tenant's external sign-in connection and its approved credentials. |
| Target owner | TenantIdentityProvider |
| Selected source owner | TenantIdentityProvider |
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
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| Issuer | string | true |
| ClientId | string | true |
| EncryptedCredentialReference | string | true |
| AllowedAudiences | IReadOnlyList<string> | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DisableTenantSignInConnection

| Selected contract detail | Value |
| --- | --- |
| Target command name | DisableTenantSignInConnection |
| Selected source command name | DisableTenantIdentityProvider |
| Name decision | Turns off external sign-in for the tenant while retaining the recorded connection. |
| Target owner | TenantIdentityProvider |
| Selected source owner | TenantIdentityProvider |
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
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/disable/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordSignInConnectionTestResult

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordSignInConnectionTestResult |
| Selected source command name | RecordTenantIdentityProviderTestResult |
| Name decision | Records whether the tenant's current sign-in connection passed; changed credentials invalidate earlier results. |
| Target owner | TenantIdentityProvider |
| Selected source owner | TenantIdentityProvider |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| ExpectedRevision | long | true |
| TestRequestEventId | Uuid | true |
| Result | TenantIdentityProviderTestResultContent | true |
| CompletedAt | DateTimeOffset | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RotateTenantSignInCredentials

| Selected contract detail | Value |
| --- | --- |
| Target command name | RotateTenantSignInCredentials |
| Selected source command name | RotateTenantIdentityProviderCredentials |
| Name decision | Replaces the tenant's sign-in credentials and invalidates earlier connection tests. |
| Target owner | TenantIdentityProvider |
| Selected source owner | TenantIdentityProvider |
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
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| EncryptedCredentialReference | string | true |
| RotationReference | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/credential-rotations/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### TestTenantSignInConnection

| Selected contract detail | Value |
| --- | --- |
| Target command name | TestTenantSignInConnection |
| Selected source command name | TestTenantIdentityProvider |
| Name decision | Tests whether the tenant's configured external sign-in connection works. |
| Target owner | TenantIdentityProvider |
| Selected source owner | TenantIdentityProvider |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | TestKey; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| ExpectedRevision | long | true |
| TestKey | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/tests/{test_key} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
