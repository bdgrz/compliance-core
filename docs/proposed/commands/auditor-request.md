# AuditorRequest commands

Selected command contracts: **11**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/auditor-request.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| AmendSampleSelection | http | SelectionId | 14 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/amend-sample-selection/{selection_id} [ApiUser + existing request-scope authorizers] |
| AssignAuditorRequest | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/assign-auditor-request/{command_id} [ApiUser + existing request-scope authorizers] |
| CreateAuditResponseDraft | http | ResponseId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/create-audit-response-draft/{response_id} [ApiUser + existing request-scope authorizers] |
| RecordAuditorRequest | http | RequestId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request [ApiUser + existing request-scope authorizers] |
| RecordAuditorRequestComment | http | CommentId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request-comment/{comment_id} [ApiUser + existing request-scope authorizers] |
| RecordSampleSelection | http | SelectionId | 12 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/record-sample-selection/{selection_id} [ApiUser + existing request-scope authorizers] |
| ReopenAuditorRequest | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/reopen-auditor-request/{command_id} [ApiUser + existing request-scope authorizers] |
| ResolveAuditorRequest | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/resolve-auditor-request/{command_id} [ApiUser + existing request-scope authorizers] |
| ReviseAuditorRequest | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/revise-auditor-request/{command_id} [ApiUser + existing request-scope authorizers] |
| AbortAuditorRequestResolution | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompleteAuditorRequestResolution | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AmendSampleSelection

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | SelectionId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorSampleSelectionAdmitted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Admit exact immutable selection on the open request before materializing child. Amendment keeps prior population identity and strictly later population version. Remove the accidental comparison of request revision to child zero. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"open request admission revision, not new selection revision zero"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/AmendSampleSelection.cs |
| Primary handler | AmendSampleSelectionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExaminationId | Uuid | true |
| AuditorRequestId | Uuid | true |
| ExpectedRequestRevision | long | true |
| SelectionId | Uuid | true |
| SupersedesSelectionId | Uuid | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationRowIds | IReadOnlyList<Uuid> | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |
| AmendmentReason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/amend-sample-selection/{selection_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AmendSampleSelectionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestTests.cs, test/Compliance.Tests/Features/AuditorRequests/SampleSelectionCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AssignAuditorRequest

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorRequestAssigned |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/AssignAuditorRequest.cs |
| Primary handler | AssignAuditorRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| AssignmentReason | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/assign-auditor-request/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AssignAuditorRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CreateAuditResponseDraft

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ResponseId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorResponseAdmitted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | None listed |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/CreateAuditResponseDraft.cs |
| Primary handler | CreateAuditResponseDraftHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ResponseId | Uuid | true |
| Summary | string | true |
| EvidenceArtifactIds | IReadOnlyList<Uuid> | true |
| ExpectedRequestRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/create-audit-response-draft/{response_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CreateAuditResponseDraftHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAuditorRequest

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | RequestId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorRequestRecorded |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | None listed |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"AuditorRequest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequest.cs |
| Primary handler | RecordAuditorRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ExaminationId | Uuid | true |
| Source | string | true |
| ExternalReference | string | true |
| Wording | string | true |
| Scope | string | true |
| Priority | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAuditorRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAuditorRequestComment

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommentId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorCommentAdmitted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommentId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequestComment.cs |
| Primary handler | RecordAuditorRequestCommentHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommentId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| Body | string | true |
| Compartment | string | true |
| ExpectedRequestRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request-comment/{comment_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAuditorRequestCommentHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordSampleSelection

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | SelectionId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorSampleSelectionAdmitted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Admit exact immutable selection on the open request before materializing child. Amendment keeps prior population identity and strictly later population version. Remove the accidental comparison of request revision to child zero. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"open request admission revision, not new selection revision zero"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/RecordSampleSelection.cs |
| Primary handler | RecordSampleSelectionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExaminationId | Uuid | true |
| AuditorRequestId | Uuid | true |
| ExpectedRequestRevision | long | true |
| SelectionId | Uuid | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationRowIds | IReadOnlyList<Uuid> | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/record-sample-selection/{selection_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordSampleSelectionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestTests.cs, test/Compliance.Tests/Features/AuditorRequests/SampleSelectionCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReopenAuditorRequest

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorRequestReopened |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/ReopenAuditorRequest.cs |
| Primary handler | ReopenAuditorRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ReopenReason | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/reopen-auditor-request/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReopenAuditorRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ResolveAuditorRequest

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorRequestResolutionAccepted |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC., ResponseIds must be nonempty, duplicate-free and exactly equal to every admitted response in this request resolution universe; freeze complete selection at admission. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/ResolveAuditorRequest.cs |
| Primary handler | ResolveAuditorRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| Resolution | string | true |
| ExpectedRevision | long | true |
| ResponseIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/resolve-auditor-request/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ResolveAuditorRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs, test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseAuditorRequest

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditorRequestRevised |
| Protocols | Response/comment admission and request resolution |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditorRequests/ReviseAuditorRequest.cs |
| Primary handler | ReviseAuditorRequestHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequestId | Uuid | true |
| ExpectedRevision | long | true |
| Wording | string | true |
| Scope | string | true |
| Priority | string | true |
| DueOn | DateOnly | true |
| ChangeReason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/revise-auditor-request/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseAuditorRequestHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortAuditorRequestResolution

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
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
| OperationId | Uuid | true |
| Target | EventStreamAddress | true |
| Generation | long | true |
| ExpectedRevision | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteAuditorRequestResolution

| Selected contract detail | Value |
| --- | --- |
| Owner | AuditorRequest |
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
| Primary handler | CompleteAuditorRequestResolutionHandler |

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
| RequestId | Uuid | true |
| ResponseProofManifest | ImmutableContentRef | true |

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
| AmendSampleSelection | AmendSampleSelectionHandler | true | authorized_http |
| AssignAuditorRequest | AssignAuditorRequestHandler | true | authorized_http |
| CreateAuditResponseDraft | CreateAuditResponseDraftHandler | true | authorized_http |
| DeliverAuditResponse | DeliverAuditResponseHandler | true | authorized_http |
| RecordAuditorRequest | RecordAuditorRequestHandler | true | authorized_http |
| RecordAuditorRequestComment | RecordAuditorRequestCommentHandler | true | authorized_http |
| RecordAuditorResponseDisposition | RecordAuditorResponseDispositionHandler | true | authorized_http |
| RecordSampleSelection | RecordSampleSelectionHandler | true | authorized_http |
| ReopenAuditorRequest | ReopenAuditorRequestHandler | true | authorized_http |
| ResolveAuditorRequest | ResolveAuditorRequestHandler | true | authorized_http |
| ReviewAuditResponse | ReviewAuditResponseHandler | true | authorized_http |
| ReviseAuditResponseDraft | ReviseAuditResponseDraftHandler | true | authorized_http |
| ReviseAuditorRequest | ReviseAuditorRequestHandler | true | authorized_http |
| SubmitAuditResponse | SubmitAuditResponseHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
