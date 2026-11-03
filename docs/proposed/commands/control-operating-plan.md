# ControlOperatingPlan commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-operating-plan.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveControlOperatingPlan | ApproveControlOperatingPlan | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ProposeControlOperatingPlan | ProposeControlOperatingPlan | http | PlanVersionId | 13 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ApproveControlOperatingPlan

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveControlOperatingPlan |
| Selected source command name | ApproveControlOperatingPlan |
| Name decision | The selected source name is retained. |
| Target owner | ControlOperatingPlan |
| Selected source owner | ControlOperatingPlan |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlOperatingPlanApproved |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOperatingPlanView","firstHttpStatus":200,"terminalResultType":"ControlOperatingPlanView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOperatingPlan","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/ApproveControlOperatingPlan.cs |
| Primary handler | ApproveControlOperatingPlanHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| PlanVersionId | Uuid | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApproveControlOperatingPlanHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs, test/Compliance.Tests/Features/Operations/PersonalOperatingPlanApprovalTransportTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs, test/Compliance.Tests/Features/Work/WorkAssignmentTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/ControlOperatingPlanWorkTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ProposeControlOperatingPlan

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeControlOperatingPlan |
| Selected source command name | ProposeControlOperatingPlan |
| Name decision | The selected source name is retained. |
| Target owner | ControlOperatingPlan |
| Selected source owner | ControlOperatingPlan |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | PlanVersionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ControlOperatingPlanProposed |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required PlanVersionId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOperatingPlanView","firstHttpStatus":200,"terminalResultType":"ControlOperatingPlanView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOperatingPlan","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/ProposeControlOperatingPlan.cs |
| Primary handler | ProposeControlOperatingPlanHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| PlanVersionId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| ControlVersionId | Uuid | true |
| Owner | OperatingHolder | true |
| BackupOwner | OperatingHolder? | true |
| ReviewerMemberId | Uuid | true |
| Cadence | ControlCadence | true |
| EffectiveFrom | DateOnly | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ProposeControlOperatingPlanHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Operations/OperationsFixture.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ApproveControlOperatingPlan | ApproveControlOperatingPlanHandler | true | personal_http |
| ProposeControlOperatingPlan | ProposeControlOperatingPlanHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
