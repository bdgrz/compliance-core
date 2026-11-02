# ControlEvaluation commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-evaluation.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| DisposeControlEvaluationDeviation | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordControlEvaluationStep | http | CommandId | 12 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviewControlEvaluation | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| StartControlEvaluation | http | EvaluationId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id} [ComplianceAuthorizationPolicies.ApiUser] |
| SubmitControlEvaluation | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### DisposeControlEvaluationDeviation

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ControlEvaluationDeviationDisposed |
| Protocols | Independent evaluation and retest |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evaluations/DisposeControlEvaluationDeviation.cs |
| Primary handler | DisposeControlEvaluationDeviationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| DeviationId | Uuid | true |
| ExpectedRevision | long | true |
| Disposition | string | true |
| Rationale | string | true |
| WaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DisposeControlEvaluationDeviationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordControlEvaluationStep

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ControlEvaluationStepRecorded |
| Protocols | Independent evaluation and retest |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evaluations/RecordControlEvaluationStep.cs |
| Primary handler | RecordControlEvaluationStepHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| StepId | Uuid | true |
| ExpectedRevision | long | true |
| Result | string | true |
| Rationale | string | true |
| InspectedItems | IReadOnlyList<EvaluationInspectedItem> | true |
| DeviationClassification | string? | false |
| DeviationDescription | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordControlEvaluationStepHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs, test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs, test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewControlEvaluation

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlEvaluationReviewed |
| Protocols | Independent evaluation and retest |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evaluations/ReviewControlEvaluation.cs |
| Primary handler | ReviewControlEvaluationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| ExpectedRevision | long | true |
| Decision | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewControlEvaluationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs, test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### StartControlEvaluation

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | EvaluationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ControlEvaluationStarted |
| Protocols | Independent evaluation and retest |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required EvaluationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}` |
| Expected revision scope | `{"field":null,"owner":"ControlEvaluation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evaluations/StartControlEvaluation.cs |
| Primary handler | StartControlEvaluationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| EvaluationId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| PlanVersionId | Uuid | true |
| RetestOfEvaluationId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| StartControlEvaluationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs, test/Compliance.Tests/Features/Evaluations/ControlEvaluationPlanHandlerTests.cs, test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs, test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs, test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### SubmitControlEvaluation

| Selected contract detail | Value |
| --- | --- |
| Owner | ControlEvaluation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlEvaluationSubmitted |
| Protocols | Independent evaluation and retest |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Evaluations/SubmitControlEvaluation.cs |
| Primary handler | SubmitControlEvaluationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| ExpectedRevision | long | true |
| Conclusions | IReadOnlyList<EvaluationAssertionConclusion> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| SubmitControlEvaluationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs, test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs, test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| DisposeControlEvaluationDeviation | DisposeControlEvaluationDeviationHandler | true | authorized_http_and_mcp |
| RecordControlEvaluationStep | RecordControlEvaluationStepHandler | true | authorized_http_and_mcp |
| ReviewControlEvaluation | ReviewControlEvaluationHandler | true | personal_http |
| StartControlEvaluation | StartControlEvaluationHandler | true | authorized_http_and_mcp |
| SubmitControlEvaluation | SubmitControlEvaluationHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
