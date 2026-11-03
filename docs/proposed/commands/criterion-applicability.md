# CriterionApplicability commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/criterion-applicability.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ProposeCriterionNotApplicable | ProposeCriterionNotApplicable | http | DecisionId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviewCriterionApplicability | ReviewCriterionApplicability | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| WithdrawCriterionNotApplicable | WithdrawCriterionNotApplicable | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ProposeCriterionNotApplicable

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeCriterionNotApplicable |
| Selected source command name | ProposeCriterionNotApplicable |
| Name decision | The selected source name is retained. |
| Target owner | CriterionApplicability |
| Selected source owner | CriterionApplicability |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | DecisionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | CriterionNotApplicableProposed |
| Protocols | Per-criterion applicability line |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required DecisionId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"CriterionApplicabilityRegistration","firstHttpStatus":200,"terminalResultType":"CriterionApplicabilityRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/ProposeCriterionNotApplicable.cs |
| Primary handler | ProposeCriterionNotApplicableHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| DecisionId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| EditionId | Uuid | true |
| CriterionIdentifier | string | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ProposeCriterionNotApplicableHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs, test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs, test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewCriterionApplicability

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewCriterionApplicability |
| Selected source command name | ReviewCriterionApplicability |
| Name decision | The selected source name is retained. |
| Target owner | CriterionApplicability |
| Selected source owner | CriterionApplicability |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | CriterionApplicabilityReviewed |
| Protocols | Per-criterion applicability line |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/ReviewCriterionApplicability.cs |
| Primary handler | ReviewCriterionApplicabilityHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| DecisionId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewCriterionApplicabilityHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs, test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### WithdrawCriterionNotApplicable

| Selected contract detail | Value |
| --- | --- |
| Target command name | WithdrawCriterionNotApplicable |
| Selected source command name | WithdrawCriterionNotApplicable |
| Name decision | The selected source name is retained. |
| Target owner | CriterionApplicability |
| Selected source owner | CriterionApplicability |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | CriterionNotApplicableWithdrawn |
| Protocols | Per-criterion applicability line |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/ControlMappings/WithdrawCriterionNotApplicable.cs |
| Primary handler | WithdrawCriterionNotApplicableHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| DecisionId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| WithdrawCriterionNotApplicableHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ProposeCriterionNotApplicable | ProposeCriterionNotApplicableHandler | true | personal_http |
| ReviewCriterionApplicability | ReviewCriterionApplicabilityHandler | true | personal_http |
| WithdrawCriterionNotApplicable | WithdrawCriterionNotApplicableHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
