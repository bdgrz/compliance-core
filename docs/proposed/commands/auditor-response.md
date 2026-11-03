# AuditorResponse commands

Selected command contracts: **8**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/auditor-response.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| DeliverAuditResponse | DeliverAuditResponse | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/deliver-audit-response/{command_id} [ApiUser + existing request-scope authorizers] |
| RecordAuditorResponseDisposition | RecordAuditorResponseDisposition | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/record-auditor-response-disposition/{command_id} [ApiUser + existing request-scope authorizers] |
| ReviewAuditResponse | ReviewAuditResponse | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/review-audit-response/{command_id} [ApiUser + existing request-scope authorizers] |
| ReviseAuditResponseDraft | ReviseAuditResponseDraft | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/revise-audit-response-draft/{command_id} [ApiUser + existing request-scope authorizers] |
| SubmitAuditResponse | SubmitAuditResponse | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/submit-audit-response/{command_id} [ApiUser + existing request-scope authorizers] |
| CreateAuditorResponseDraft | MaterializeAuditorResponse | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| AuthorizeAuditorResponseRequestResolution | AuthorizeAuditorResponseRequestResolutionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CloseAuditorResponseRequestResolutionAuthorization | SettleAuditorResponseRequestResolutionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### DeliverAuditResponse

| Selected contract detail | Value |
| --- | --- |
| Target command name | DeliverAuditResponse |
| Selected source command name | DeliverAuditResponse |
| Name decision | The selected source name is retained. |
| Owner | AuditorResponse |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorResponseDelivered |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/DeliverAuditResponse.cs |
| Primary handler | DeliverAuditResponseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| Version | long | true |
| Recipient | string | true |
| DeliveryReference | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/deliver-audit-response/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DeliverAuditResponseHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAuditorResponseDisposition

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAuditorResponseDisposition |
| Selected source command name | RecordAuditorResponseDisposition |
| Name decision | The selected source name is retained. |
| Owner | AuditorResponse |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorResponseDispositionRecorded |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/RecordAuditorResponseDisposition.cs |
| Primary handler | RecordAuditorResponseDispositionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| Version | long | true |
| Disposition | string | true |
| ExternalReference | string? | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/record-auditor-response-disposition/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAuditorResponseDispositionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewAuditResponse

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewAuditResponse |
| Selected source command name | ReviewAuditResponse |
| Name decision | The selected source name is retained. |
| Owner | AuditorResponse |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorResponseDisclosureReviewed |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/ReviewAuditResponse.cs |
| Primary handler | ReviewAuditResponseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| Version | long | true |
| Outcome | string | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/review-audit-response/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewAuditResponseHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseAuditResponseDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseAuditResponseDraft |
| Selected source command name | ReviseAuditResponseDraft |
| Name decision | The selected source name is retained. |
| Owner | AuditorResponse |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditResponseDraftRevised |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/ReviseAuditResponseDraft.cs |
| Primary handler | ReviseAuditResponseDraftHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| ExpectedRevision | long | true |
| Summary | string | true |
| EvidenceArtifactIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/revise-audit-response-draft/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseAuditResponseDraftHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### SubmitAuditResponse

| Selected contract detail | Value |
| --- | --- |
| Target command name | SubmitAuditResponse |
| Selected source command name | SubmitAuditResponse |
| Name decision | The selected source name is retained. |
| Owner | AuditorResponse |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorResponseSubmitted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/SubmitAuditResponse.cs |
| Primary handler | SubmitAuditResponseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| Version | long | true |
| SubmissionNote | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/submit-audit-response/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| SubmitAuditResponseHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CreateAuditorResponseDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateAuditorResponseDraft |
| Selected source command name | MaterializeAuditorResponse |
| Name decision | Creates the exact initial response draft admitted for an auditor request. |
| Owner | AuditorResponse |
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
| Primary handler | MaterializeAuditorResponseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AuditorResponseAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeAuditorResponseRequestResolution

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeAuditorResponseRequestResolution |
| Selected source command name | AuthorizeAuditorResponseRequestResolutionUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | AuditorResponse |
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
| Primary handler | AuthorizeAuditorResponseRequestResolutionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AuditorRequestResolutionAccepted> | true |
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

### CloseAuditorResponseRequestResolutionAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseAuditorResponseRequestResolutionAuthorization |
| Selected source command name | SettleAuditorResponseRequestResolutionUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | AuditorResponse |
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
| Primary handler | SettleAuditorResponseRequestResolutionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleAuditorResponseRequestResolutionUseSourceProof | true |
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
| DeliverAuditResponse | DeliverAuditResponseHandler | true | authorized_http |
| RecordAuditorResponseDisposition | RecordAuditorResponseDispositionHandler | true | authorized_http |
| ReviewAuditResponse | ReviewAuditResponseHandler | true | authorized_http |
| ReviseAuditResponseDraft | ReviseAuditResponseDraftHandler | true | authorized_http |
| SubmitAuditResponse | SubmitAuditResponseHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
