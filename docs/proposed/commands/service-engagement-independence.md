# ServiceEngagementIndependence commands



Selected command contracts: **17**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/service-engagement-independence.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AcceptServiceEngagement | AcceptServiceEngagement | http | CommandId | 4 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AmendServiceEngagement | AmendServiceEngagement | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/amend-service-engagement/{command_id} [ApiUser + existing request-scope authorizers] |
| AssignServiceEngagementStaff | AssignServiceEngagementStaff | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CloseServiceEngagement | CloseServiceEngagement | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CreateServiceEngagement | CreateServiceEngagement | http | EngagementId | 4 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/create-service-engagement [ApiUser + existing request-scope authorizers] |
| ProposeServiceEngagementStaff | ProposeServiceEngagementStaff | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id} [ApiUser + existing request-scope authorizers] |
| RecordDirectoryIndependenceReevaluation | RecordDirectoryIndependenceReevaluation | command | CommandId | 13 | [Placeholder: value not recorded] |
| RevokeServiceEngagementActualStaff | RevokeServiceEngagementActualStaff | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| WithdrawServiceEngagementStaffProposal | WithdrawServiceEngagementStaffProposal | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RejectServiceEngagementAcceptance | RejectServiceEngagementAcceptance | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| RejectServiceEngagementStaffAssignment | RejectServiceEngagementStaffAssignment | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CancelEngagementPersonalDecision | CancelEngagementPersonalDecision | abort | CommandId | 5 | [Placeholder: route/channel not recorded] |
| CompleteServiceEngagementAcceptance | CompleteServiceEngagementAcceptance | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CompleteServiceEngagementStaffAssignment | CompleteServiceEngagementStaffAssignment | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| AuthorizeEngagementPracticeCommitments | AuthorizeEngagementPracticeCommitments | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| RecordEngagementPersonalDecision | SettleEngagementPersonalDecision | receiver | CommandId | 8 | [Placeholder: value not recorded] |
| AuthorizeEngagementPersonalDecision | AuthorizeEngagementPersonalDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AcceptServiceEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcceptServiceEngagement |
| Selected source command name | AcceptServiceEngagement |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ServiceEngagementAcceptanceRequested |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ServiceEngagementAcceptanceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ServiceEngagementAcceptanceAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/AcceptServiceEngagement.cs |
| Primary handler | AcceptServiceEngagementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AcceptServiceEngagementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AmendServiceEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | AmendServiceEngagement |
| Selected source command name | AmendServiceEngagement |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementMutationRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/AmendServiceEngagement.cs |
| Primary handler | AmendServiceEngagementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| ExpectedRevision | long | true |
| Content | ServiceEngagementDraftContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/amend-service-engagement/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AmendServiceEngagementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AssignServiceEngagementStaff

| Selected contract detail | Value |
| --- | --- |
| Target command name | AssignServiceEngagementStaff |
| Selected source command name | AssignServiceEngagementStaff |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ServiceEngagementStaffAssignmentRequested |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision., Positive actual-staff assignment acquires the same exact current service-epoch, partner authority and permanent staff/user practice commitments as acceptance before emitting ServiceEngagementStaffAssigned. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ServiceEngagementAcceptanceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ProfessionalEngagementPartnerAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/AssignServiceEngagementStaff.cs |
| Primary handler | AssignServiceEngagementStaffHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| StaffMemberId | Uuid | true |
| ExpectedRevision | long | true |
| EffectiveFrom | DateOnly | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AssignServiceEngagementStaffHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CloseServiceEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseServiceEngagement |
| Selected source command name | CloseServiceEngagement |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementMutationRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/CloseServiceEngagement.cs |
| Primary handler | CloseServiceEngagementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CloseServiceEngagementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs, test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CreateServiceEngagement

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateServiceEngagement |
| Selected source command name | CreateServiceEngagement |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | EngagementId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementMutationRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","meaning":"new independent owner creation requires zero; former directory/tenant/system ledger revision is not reused"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/CreateServiceEngagement.cs |
| Primary handler | CreateServiceEngagementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| ExpectedRevision | long | true |
| Content | ServiceEngagementDraftContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/create-service-engagement | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CreateServiceEngagementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/IndependenceAssignmentBoundaryTests.cs, test/Compliance.Tests/Features/AccessControl/ServiceEngagementHandlerTests.cs, test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ProposeServiceEngagementStaff

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeServiceEngagementStaff |
| Selected source command name | ProposeServiceEngagementStaff |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementMutationRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/ProposeServiceEngagementStaff.cs |
| Primary handler | ProposeServiceEngagementStaffHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| StaffMemberId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ProposeServiceEngagementStaffHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/IndependenceAssignmentBoundaryTests.cs, test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordDirectoryIndependenceReevaluation

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordDirectoryIndependenceReevaluation |
| Selected source command name | RecordDirectoryIndependenceReevaluation |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; source_event_effect; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | internal_trusted_command |
| MCP | false |
| Selected semantic events | DirectoryIndependenceReevaluated |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Target one engagement acceptance pair; discovery/fan-out belongs to the reactor, not a tenant-wide mutation. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"internal","responseType":"Unit","terminalResultType":"Unit"}` |
| Expected revision scope | `{"owner":"ServiceEngagementIndependence","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluationAuthorizer.cs |
| Source contract | src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs |
| Primary handler | RecordDirectoryIndependenceReevaluationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| DirectoryResourceOffset | ulong | true |
| DirectoryEventId | Uuid | true |
| DirectoryPayloadSha256 | string | true |
| AcceptanceResourceOffset | ulong | true |
| AcceptanceEventId | Uuid | true |
| AcceptanceRequestId | Uuid | true |
| AcceptancePayloadSha256 | string | true |
| StaffMemberId | Uuid | true |
| UserId | Uuid | true |
| EngagementId | Uuid | true |
| AcceptedServiceEpoch | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordDirectoryIndependenceReevaluationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/DirectoryIndependenceReevaluationTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RevokeServiceEngagementActualStaff

| Selected contract detail | Value |
| --- | --- |
| Target command name | RevokeServiceEngagementActualStaff |
| Selected source command name | RevokeServiceEngagementActualStaff |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementAssignmentRevoked |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementAcceptanceView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementAcceptanceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/RevokeServiceEngagementActualStaff.cs |
| Primary handler | RevokeServiceEngagementActualStaffHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| StaffMemberId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RevokeServiceEngagementActualStaffHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs, test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### WithdrawServiceEngagementStaffProposal

| Selected contract detail | Value |
| --- | --- |
| Target command name | WithdrawServiceEngagementStaffProposal |
| Selected source command name | WithdrawServiceEngagementStaffProposal |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ServiceEngagementMutationRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/WithdrawServiceEngagementStaffProposal.cs |
| Primary handler | WithdrawServiceEngagementStaffProposalHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EngagementId | Uuid | true |
| StaffMemberId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| WithdrawServiceEngagementStaffProposalHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RejectServiceEngagementAcceptance

| Selected contract detail | Value |
| --- | --- |
| Target command name | RejectServiceEngagementAcceptance |
| Selected source command name | RejectServiceEngagementAcceptance |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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

### RejectServiceEngagementStaffAssignment

| Selected contract detail | Value |
| --- | --- |
| Target command name | RejectServiceEngagementStaffAssignment |
| Selected source command name | RejectServiceEngagementStaffAssignment |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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

### CancelEngagementPersonalDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelEngagementPersonalDecision |
| Selected source command name | CancelEngagementPersonalDecision |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| RootOperationId | Uuid | true |
| EngagementId | Uuid | true |
| Generation | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteServiceEngagementAcceptance

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteServiceEngagementAcceptance |
| Selected source command name | CompleteServiceEngagementAcceptance |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| Primary handler | CompleteServiceEngagementAcceptanceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<EngagementPracticeCommitmentsAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| ServiceEpoch | long | true |
| CommitmentProofManifest | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteServiceEngagementStaffAssignment

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteServiceEngagementStaffAssignment |
| Selected source command name | CompleteServiceEngagementStaffAssignment |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| Primary handler | CompleteServiceEngagementStaffAssignmentHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<EngagementPracticeCommitmentsAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| ServiceEpoch | long | true |
| CommitmentProofManifest | ImmutableContentRef | true |
| StaffMemberId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeEngagementPracticeCommitments

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeEngagementPracticeCommitments |
| Selected source command name | AuthorizeEngagementPracticeCommitments |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| Primary handler | AuthorizeEngagementPracticeCommitmentsHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AuthorizeEngagementPracticeCommitmentsSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ReservationProofManifest | ImmutableContentRef | true |
| SourceUseProofManifest | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordEngagementPersonalDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordEngagementPersonalDecision |
| Selected source command name | SettleEngagementPersonalDecision |
| Name decision | Records the already authorized management acknowledgement or partner independence evaluation on the exact engagement. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| Primary handler | SettleEngagementPersonalDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleEngagementPersonalDecisionSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeEngagementPersonalDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeEngagementPersonalDecision |
| Selected source command name | AuthorizeEngagementPersonalDecision |
| Name decision | The selected source name is retained. |
| Target owner | ServiceEngagementIndependence |
| Selected source owner | ServiceEngagementIndependence |
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
| Primary handler | AuthorizeEngagementPersonalDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<EngagementPersonalDecisionAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| SourceUseProofs | ImmutableContentRef | true |

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
| AcceptServiceEngagement | AcceptServiceEngagementHandler | true | personal_http |
| AcknowledgeEngagementManagement | AcknowledgeEngagementManagementHandler | true | personal_http |
| AmendServiceEngagement | AmendServiceEngagementHandler | true | authorized_http_and_mcp |
| AssignServiceEngagementStaff | AssignServiceEngagementStaffHandler | true | authorized_http |
| CloseServiceEngagement | CloseServiceEngagementHandler | true | authorized_http_and_mcp |
| CreateServiceEngagement | CreateServiceEngagementHandler | true | authorized_http_and_mcp |
| ProposeServiceEngagementStaff | ProposeServiceEngagementStaffHandler | true | authorized_http_and_mcp |
| RecordDirectoryIndependenceReevaluation | RecordDirectoryIndependenceReevaluationHandler | true | internal_trusted_command |
| RecordPartnerIndependenceEvaluation | RecordPartnerIndependenceEvaluationHandler | true | authorized_http |
| RevokeServiceEngagementActualStaff | RevokeServiceEngagementActualStaffHandler | true | authorized_http_and_mcp |
| WithdrawServiceEngagementStaffProposal | WithdrawServiceEngagementStaffProposalHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
