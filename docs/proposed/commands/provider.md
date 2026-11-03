# Provider commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/provider.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordProvider | RecordProvider | http | ProviderId | 3 | PUT /api/v1/tenants/{tenant_id}/providers/{provider_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseProvider | ReviseProvider | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/providers/revise-provider/{command_id} [ApiUser + existing request-scope authorizers] |
| CancelProviderChange | CancelProviderChange | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| ApplyProviderChange | ApplyProviderChange | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### RecordProvider

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordProvider |
| Selected source command name | RecordProvider |
| Name decision | The selected source name is retained. |
| Owner | Provider |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ProviderId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ProviderChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ProviderId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"Provider","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/RecordProvider.cs |
| Primary handler | RecordProviderHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ProviderId | Uuid | true |
| TenantId | Uuid | true |
| Content | ProviderContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/{provider_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordProviderHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs, test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs, test/Compliance.Tests/Features/Providers/AttestProviderManagementWriteWallTests.cs, test/Compliance.Tests/Features/Providers/ProviderWriteAuthorizationProbeHandler.cs, test/Compliance.Tests/Features/Providers/ProviderAuthorizationTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseProvider

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseProvider |
| Selected source command name | ReviseProvider |
| Name decision | The selected source name is retained. |
| Owner | Provider |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ProviderChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"Provider","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/ReviseProvider.cs |
| Primary handler | ReviseProviderHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| ExpectedRevision | long | true |
| Content | ProviderContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/revise-provider/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseProviderHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs, test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs, test/Compliance.Tests/Features/Providers/AttestProviderManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CancelProviderChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelProviderChange |
| Selected source command name | CancelProviderChange |
| Name decision | The selected source name is retained. |
| Owner | Provider |
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
| TenantId | Uuid | true |
| RecordId | Uuid | true |
| ChangeOperationId | Uuid | true |
| ExpectedRevision | long | true |
| DefiniteFailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyProviderChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApplyProviderChange |
| Selected source command name | ApplyProviderChange |
| Name decision | The selected source name is retained. |
| Owner | Provider |
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
| Primary handler | ApplyProviderChangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ProviderNameClaimed> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| RecordId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| RecordProvider | RecordProviderHandler | true | authorized_http_and_mcp |
| ReviseProvider | ReviseProviderHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
