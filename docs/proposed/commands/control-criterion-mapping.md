# ControlCriterionMapping commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-criterion-mapping.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ProposeControlCriterionMapping | ProposeControlCriterionMapping | http | MappingId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RetireControlCriterionMapping | RetireControlCriterionMapping | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviewControlCriterionMapping | ReviewControlCriterionMapping | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ProposeControlCriterionMapping

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeControlCriterionMapping |
| Selected source command name | ProposeControlCriterionMapping |
| Name decision | The selected source name is retained. |
| Owner | ControlCriterionMapping |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | MappingId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ControlCriterionMappingProposed |
| Protocols | Per-tuple mapping succession |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required MappingId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlCriterionMappingRegistration","firstHttpStatus":200,"terminalResultType":"ControlCriterionMappingRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/ProposeControlCriterionMapping.cs |
| Primary handler | ProposeControlCriterionMappingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| MappingId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ControlVersionId | Uuid | true |
| EditionId | Uuid | true |
| CriterionIdentifier | string | true |
| ExpectedRevision | long | true |
| Rationale | string | true |
| ApplicabilityExplanation | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ProposeControlCriterionMappingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs, test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RetireControlCriterionMapping

| Selected contract detail | Value |
| --- | --- |
| Target command name | RetireControlCriterionMapping |
| Selected source command name | RetireControlCriterionMapping |
| Name decision | The selected source name is retained. |
| Owner | ControlCriterionMapping |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ControlCriterionMappingRetired |
| Protocols | Per-tuple mapping succession |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/RetireControlCriterionMapping.cs |
| Primary handler | RetireControlCriterionMappingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| MappingId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RetireControlCriterionMappingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewControlCriterionMapping

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewControlCriterionMapping |
| Selected source command name | ReviewControlCriterionMapping |
| Name decision | The selected source name is retained. |
| Owner | ControlCriterionMapping |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlCriterionMappingReviewed |
| Protocols | Per-tuple mapping succession |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/ReviewControlCriterionMapping.cs |
| Primary handler | ReviewControlCriterionMappingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| MappingId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewControlCriterionMappingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs, test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ProposeControlCriterionMapping | ProposeControlCriterionMappingHandler | true | authorized_http |
| RetireControlCriterionMapping | RetireControlCriterionMappingHandler | true | authorized_http |
| ReviewControlCriterionMapping | ReviewControlCriterionMappingHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
