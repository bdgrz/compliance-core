# Location commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/location.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordLocation | RecordLocation | http | LocationId | 6 | PUT /api/v1/tenants/{tenant_id}/locations/{location_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseLocation | ReviseLocation | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/locations/revise-location/{command_id} [ApiUser + existing request-scope authorizers] |
| CancelLocationChange | CancelLocationChange | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| ApplyLocationChange | ApplyLocationChange | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### RecordLocation

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordLocation |
| Selected source command name | RecordLocation |
| Name decision | The selected source name is retained. |
| Owner | Location |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | LocationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | LocationChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required LocationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"LocationRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"Location","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/TechnologyInventory/TechnologyInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/TechnologyInventory/RecordLocation.cs |
| Primary handler | RecordLocationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| LocationId | Uuid | true |
| TenantId | Uuid | true |
| Kind | string | true |
| Name | string | true |
| OwnerPersonId | Uuid | true |
| GeographyReference | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/locations/{location_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordLocationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseLocation

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseLocation |
| Selected source command name | ReviseLocation |
| Name decision | The selected source name is retained. |
| Owner | Location |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | LocationChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"Location","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/TechnologyInventory/TechnologyInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/TechnologyInventory/ReviseLocation.cs |
| Primary handler | ReviseLocationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| LocationId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| OwnerPersonId | Uuid | true |
| Lifecycle | string | true |
| GeographyReference | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/locations/revise-location/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseLocationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CancelLocationChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelLocationChange |
| Selected source command name | CancelLocationChange |
| Name decision | The selected source name is retained. |
| Owner | Location |
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

### ApplyLocationChange

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApplyLocationChange |
| Selected source command name | ApplyLocationChange |
| Name decision | The selected source name is retained. |
| Owner | Location |
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
| Primary handler | ApplyLocationChangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<LocationNameClaimed> | true |
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
| RecordLocation | RecordLocationHandler | true | authorized_http_and_mcp |
| ReviseLocation | ReviseLocationHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
