# RiskTreatmentAction commands



Selected command contracts: **7**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/risk-treatment-action.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AddRiskTreatmentAction | AddRiskTreatmentAction | http | ActionId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CancelRiskTreatmentAction | CancelRiskTreatmentAction | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviewRiskTreatmentActionCompletion | ReviewRiskTreatmentActionCompletion | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseRiskTreatmentAction | ReviseRiskTreatmentAction | http | CommandId | 12 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/revise-risk-treatment-action/{command_id} [ApiUser + existing request-scope authorizers] |
| SubmitRiskTreatmentActionCompletion | SubmitRiskTreatmentActionCompletion | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AbortRiskTreatmentActionReview | AbortRiskTreatmentActionReview | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompleteRiskTreatmentActionReview | CompleteRiskTreatmentActionReview | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AddRiskTreatmentAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | AddRiskTreatmentAction |
| Selected source command name | AddRiskTreatmentAction |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ActionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | RiskTreatmentActionAdded |
| Protocols | Risk ownership, treatment lines and actions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ActionId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"RiskTreatmentActionRegistration","firstHttpStatus":200,"terminalResultType":"RiskTreatmentActionRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","meaning":"new independent action creation requires zero; old program risk-governance ledger revision removed"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Risks/AddRiskTreatmentAction.cs |
| Primary handler | AddRiskTreatmentActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ActionId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| ExpectedRevision | long | true |
| Title | string | true |
| TargetState | string | true |
| ExpectedEvidence | string | true |
| DueOn | DateOnly | true |
| AccountableMemberId | Uuid | true |
| EvidenceRequestIds | IReadOnlyList<Uuid>? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AddRiskTreatmentActionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CancelRiskTreatmentAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelRiskTreatmentAction |
| Selected source command name | CancelRiskTreatmentAction |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | RiskTreatmentActionCancelled |
| Protocols | Risk ownership, treatment lines and actions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Risks/CancelRiskTreatmentAction.cs |
| Primary handler | CancelRiskTreatmentActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| ActionId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CancelRiskTreatmentActionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewRiskTreatmentActionCompletion

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewRiskTreatmentActionCompletion |
| Selected source command name | ReviewRiskTreatmentActionCompletion |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | RiskTreatmentActionCompletionReviewAccepted |
| Protocols | Risk ownership, treatment lines and actions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Risks/ReviewRiskTreatmentActionCompletion.cs |
| Primary handler | ReviewRiskTreatmentActionCompletionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| ActionId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewRiskTreatmentActionCompletionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs, test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseRiskTreatmentAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseRiskTreatmentAction |
| Selected source command name | ReviseRiskTreatmentAction |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | RiskTreatmentActionRevised |
| Protocols | Risk ownership, treatment lines and actions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Risks/ReviseRiskTreatmentAction.cs |
| Primary handler | ReviseRiskTreatmentActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| ActionId | Uuid | true |
| ExpectedRevision | long | true |
| Title | string | true |
| TargetState | string | true |
| ExpectedEvidence | string | true |
| DueOn | DateOnly | true |
| AccountableMemberId | Uuid | true |
| EvidenceRequestIds | IReadOnlyList<Uuid>? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/revise-risk-treatment-action/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseRiskTreatmentActionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### SubmitRiskTreatmentActionCompletion

| Selected contract detail | Value |
| --- | --- |
| Target command name | SubmitRiskTreatmentActionCompletion |
| Selected source command name | SubmitRiskTreatmentActionCompletion |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | RiskTreatmentActionCompletionSubmitted |
| Protocols | Risk ownership, treatment lines and actions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"RiskTreatmentActionCompletionRegistration","firstHttpStatus":200,"terminalResultType":"RiskTreatmentActionCompletionRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Risks/SubmitRiskTreatmentActionCompletion.cs |
| Primary handler | SubmitRiskTreatmentActionCompletionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| ActionId | Uuid | true |
| ExpectedRevision | long | true |
| Summary | string | true |
| EvidenceRequestIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| SubmitRiskTreatmentActionCompletionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortRiskTreatmentActionReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | AbortRiskTreatmentActionReview |
| Selected source command name | AbortRiskTreatmentActionReview |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
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

### CompleteRiskTreatmentActionReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteRiskTreatmentActionReview |
| Selected source command name | CompleteRiskTreatmentActionReview |
| Name decision | The selected source name is retained. |
| Target owner | RiskTreatmentAction |
| Selected source owner | RiskTreatmentAction |
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
| Primary handler | CompleteRiskTreatmentActionReviewHandler |

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
| ActionId | Uuid | true |
| EvidenceUseProofs | ImmutableContentRef | true |

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
| AddRiskTreatmentAction | AddRiskTreatmentActionHandler | true | authorized_http |
| CancelRiskTreatmentAction | CancelRiskTreatmentActionHandler | true | authorized_http |
| ReviewRiskTreatmentActionCompletion | ReviewRiskTreatmentActionCompletionHandler | true | personal_http |
| ReviseRiskTreatmentAction | ReviseRiskTreatmentActionHandler | true | authorized_http |
| SubmitRiskTreatmentActionCompletion | SubmitRiskTreatmentActionCompletionHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
