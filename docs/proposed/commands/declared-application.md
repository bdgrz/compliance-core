# DeclaredApplication commands

Selected command contracts: **8**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/declared-application.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| ApplyApplicationImportEffect | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| DeclareApplication | http | ApplicationId | 11 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RetireApplication | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/retirements/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseApplication | http | CommandId | 16 | PUT /api/v1/tenants/{tenant_id}/applications/revise-application/{command_id} [ApiUser + existing request-scope authorizers] |
| ReserveApplicationImportTarget | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| FenceApplicationImportTarget | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| ReleaseApplicationImportTarget | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| ApplyApplicationNameChange | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### ApplyApplicationImportEffect

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
| Contract record status | target_contract |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; source_event_effect; UUIDv5(source eventId, length-prefixed effectKind + canonical target stream + source lifecycle episode); external process callback source is the immutable accepted business source event plus exact effect key, never attemptId. |
| Entry point | internal_trusted_command |
| MCP | false |
| Selected semantic events | ApplicationImportEffectPending |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event., Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":"Explicit null requires exact source authorization/generation and physical append OCC; never substitute current logical revision."}` |
| Authorizer sources | src/Compliance.Core/Features/Applications/ApplyApplicationImportEffectAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/ApplyApplicationImportEffect.cs |
| Primary handler | ApplyApplicationImportEffectHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ApplicationImportExecutionAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| RowGroup | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApplyApplicationImportEffectHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportPendingEffectTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportEffectVerificationTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### DeclareApplication

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ApplicationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ApplicationDeclared |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ApplicationId., Remove optional DeclarationKey; ApplicationId is the required creation key. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ApplicationRegistration","firstHttpStatus":200,"terminalResultType":"ApplicationRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"DeclaredApplication","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/DeclareApplication.cs |
| Primary handler | DeclareApplicationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ApplicationId | Uuid | true |
| TenantId | Uuid | true |
| Name | string | true |
| Purpose | string | true |
| OwnerReference | string? | false |
| Classification | string? | false |
| SystemOwnerPersonId | Uuid? | false |
| AccessOwnerPersonId | Uuid? | false |
| IsRestricted | bool | false |
| ApplicationKind | string? | false |
| Criticality | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DeclareApplicationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs, test/Compliance.Tests/Features/Applications/PersonalAccessReviewScopeTransportTests.cs, test/Compliance.Tests/Features/Applications/ApplicationOwnerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs, test/Compliance.Tests/Features/Examinations/ReviewedSystemTypeIBaselineSourceContributorTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RetireApplication

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ApplicationRetired |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/RetireApplication.cs |
| Primary handler | RetireApplicationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| ExpectedRevision | long | true |
| EffectiveAt | DateTimeOffset | true |
| Reason | string | true |
| MergedIntoApplicationId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/retirements/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RetireApplicationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseApplication

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ApplicationRevised |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Include init-only authored scope and field-presence semantics omitted by the earlier positional-record scan. JsonIgnore presence flags are adapter-owned and canonicalized explicitly; never serialize/replay them as if false meant omitted. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/ReviseApplication.cs |
| Primary handler | ReviseApplicationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| Purpose | string | true |
| OwnerReference | string? | true |
| Classification | string? | false |
| SystemOwnerPersonId | Uuid? | false |
| AccessOwnerPersonId | Uuid? | false |
| IsRestricted | bool? | false |
| ApplicationKind | string? | false |
| Criticality | string? | false |
| ClassificationIntent | ApplicationRevisionField | false |
| SystemOwnerIntent | ApplicationRevisionField | false |
| AccessOwnerIntent | ApplicationRevisionField | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/revise-application/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseApplicationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRevisionFieldPresenceHttpTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRevisionPreviewContractTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRevisionGovernanceTests.cs, test/Compliance.Tests/Features/Applications/ApplicationOwnerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReserveApplicationImportTarget

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
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
| Primary handler | ReserveApplicationImportTargetHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ApplicationImportExecutionAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| GroupedRows | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### FenceApplicationImportTarget

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
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
| Primary handler | FenceApplicationImportTargetHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ApplicationImportCancellationAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReleaseApplicationImportTarget

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
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
| Primary handler | ReleaseApplicationImportTargetHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | ReleaseApplicationImportTargetSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyApplicationNameChange

| Selected contract detail | Value |
| --- | --- |
| Owner | DeclaredApplication |
| Contract record status | PLANNED |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
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
| CommandId | [Placeholder: value not recorded] | true |
| Source | [Placeholder: value not recorded] | true |
| Target | [Placeholder: value not recorded] | true |
| OperationId | [Placeholder: value not recorded] | true |
| Generation | [Placeholder: value not recorded] | true |
| ExpectedRevision | [Placeholder: value not recorded] | true |
| Intent | [Placeholder: value not recorded] | true |
| ApplicationId | [Placeholder: value not recorded] | true |

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
| AddApplicationRelationship | AddApplicationRelationshipHandler | true | authorized_http |
| ApplyApplicationImportEffect | ApplyApplicationImportEffectHandler | true | internal_trusted_command |
| ApproveApplicationSuccessor | ApproveApplicationSuccessorHandler | true | personal_http |
| DeclareApplication | DeclareApplicationHandler | true | authorized_http_and_mcp |
| RemoveApplicationRelationship | RemoveApplicationRelationshipHandler | true | authorized_http_and_mcp |
| RetireApplication | RetireApplicationHandler | true | authorized_http_and_mcp |
| ReviseApplication | ReviseApplicationHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
