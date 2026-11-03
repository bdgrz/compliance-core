# EvidenceRequest commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/evidence-request.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CancelEvidenceRequest | CancelEvidenceRequest | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| FulfilEvidenceRequest | FulfilEvidenceRequest | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| OpenEvidenceRequest | OpenEvidenceRequest | http | EvidenceRequestId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AuthorizeEvidenceRequestActionCompletion | AuthorizeEvidenceRequestActionCompletionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CloseEvidenceRequestActionCompletionAuthorization | SettleEvidenceRequestActionCompletionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### CancelEvidenceRequest

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelEvidenceRequest |
| Selected source command name | CancelEvidenceRequest |
| Name decision | The selected source name is retained. |
| Owner | EvidenceRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | EvidenceRequestCancelled |
| Protocols | Independent evidence fulfilment |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"EvidenceRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evidence/CancelEvidenceRequest.cs |
| Primary handler | CancelEvidenceRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| EvidenceRequestId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CancelEvidenceRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs, test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs, test/Compliance.Tests/Features/Work/WorkQueueTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### FulfilEvidenceRequest

| Selected contract detail | Value |
| --- | --- |
| Target command name | FulfilEvidenceRequest |
| Selected source command name | FulfilEvidenceRequest |
| Name decision | The selected source name is retained. |
| Owner | EvidenceRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | EvidenceRequestFulfilled |
| Protocols | Independent evidence fulfilment |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"EvidenceRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evidence/FulfilEvidenceRequest.cs |
| Primary handler | FulfilEvidenceRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| EvidenceRequestId | Uuid | true |
| ExpectedRevision | long | true |
| ArtifactId | Uuid | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| FulfilEvidenceRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### OpenEvidenceRequest

| Selected contract detail | Value |
| --- | --- |
| Target command name | OpenEvidenceRequest |
| Selected source command name | OpenEvidenceRequest |
| Name decision | The selected source name is retained. |
| Owner | EvidenceRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | EvidenceRequestId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | EvidenceRequestOpened |
| Protocols | Independent evidence fulfilment |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required EvidenceRequestId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}` |
| Expected revision scope | `{"field":null,"owner":"EvidenceRequest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evidence/OpenEvidenceRequest.cs |
| Primary handler | OpenEvidenceRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| EvidenceRequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Title | string | true |
| Instructions | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| ControlId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| OpenEvidenceRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs, test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs, test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs, test/Compliance.Tests/Features/Work/WorkQueueTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AuthorizeEvidenceRequestActionCompletion

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeEvidenceRequestActionCompletion |
| Selected source command name | AuthorizeEvidenceRequestActionCompletionUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | EvidenceRequest |
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
| Primary handler | AuthorizeEvidenceRequestActionCompletionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<RiskTreatmentActionCompletionReviewAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| SourceRevision | ulong | true |
| ReceivingOwnerId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CloseEvidenceRequestActionCompletionAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseEvidenceRequestActionCompletionAuthorization |
| Selected source command name | SettleEvidenceRequestActionCompletionUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | EvidenceRequest |
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
| Primary handler | SettleEvidenceRequestActionCompletionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleEvidenceRequestActionCompletionUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| ReceivingOwnerId | Uuid | true |
| Disposition | SourceUseSettlementDisposition | true |

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
| CancelEvidenceRequest | CancelEvidenceRequestHandler | true | authorized_http_and_mcp |
| FulfilEvidenceRequest | FulfilEvidenceRequestHandler | true | authorized_http |
| OpenEvidenceRequest | OpenEvidenceRequestHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
