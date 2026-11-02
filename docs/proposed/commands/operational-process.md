# OperationalProcess commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/operational-process.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| RecordOperationalProcess | http | OperationalProcessId | 7 | PUT /api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseOperationalProcess | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/operational-processes/revise-operational-process/{command_id} [ApiUser + existing request-scope authorizers] |
| CancelOperationalProcessChange | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| ApplyOperationalProcessChange | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### RecordOperationalProcess

| Selected contract detail | Value |
| --- | --- |
| Owner | OperationalProcess |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | OperationalProcessId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | OperationalProcessChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required OperationalProcessId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"OperationalProcessRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"OperationalProcess","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/TechnologyInventory/TechnologyInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/TechnologyInventory/RecordOperationalProcess.cs |
| Primary handler | RecordOperationalProcessHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| OperationalProcessId | Uuid | true |
| TenantId | Uuid | true |
| Name | string | true |
| Purpose | string | true |
| OwnerPersonId | Uuid | true |
| Inputs | string? | false |
| Outputs | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordOperationalProcessHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseOperationalProcess

| Selected contract detail | Value |
| --- | --- |
| Owner | OperationalProcess |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | OperationalProcessChangeAccepted |
| Protocols | Name claims and record lifecycle |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"OperationalProcess","counter":"named process record revision","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/TechnologyInventory/TechnologyInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/TechnologyInventory/ReviseOperationalProcess.cs |
| Primary handler | ReviseOperationalProcessHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| OperationalProcessId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| Purpose | string | true |
| OwnerPersonId | Uuid | true |
| Lifecycle | string | true |
| Inputs | string? | false |
| Outputs | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/operational-processes/revise-operational-process/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseOperationalProcessHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CancelOperationalProcessChange

| Selected contract detail | Value |
| --- | --- |
| Owner | OperationalProcess |
| Contract record status | [Placeholder: value not recorded] |
| Kind | abort |
| Logical identity | CommandId; Uuid; required=true; abort receiver identity bound to the exact admitted operation and generation: exact redelivery resolves the original rejected result; an abort after a terminal outcome is a no-op |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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

### ApplyOperationalProcessChange

| Selected contract detail | Value |
| --- | --- |
| Owner | OperationalProcess |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | ApplyOperationalProcessChangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<OperationalProcessNameClaimed> | true |
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

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| RecordOperationalProcess | RecordOperationalProcessHandler | true | authorized_http_and_mcp |
| ReviseOperationalProcess | ReviseOperationalProcessHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
