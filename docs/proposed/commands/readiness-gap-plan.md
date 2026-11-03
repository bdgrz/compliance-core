# ReadinessGapPlan commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/readiness-gap-plan.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AnnotateReadinessGap | AnnotateReadinessGap | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| PlanReadinessGap | PlanReadinessGap | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan-readiness-gap/{command_id} [ApiUser + existing request-scope authorizers] |
| AuthorizeReadinessGapPlanReadinessDecision | AuthorizeReadinessGapPlanReadinessDecisionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CloseReadinessGapPlanReadinessDecisionAuthorization | SettleReadinessGapPlanReadinessDecisionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### AnnotateReadinessGap

| Selected contract detail | Value |
| --- | --- |
| Target command name | AnnotateReadinessGap |
| Selected source command name | AnnotateReadinessGap |
| Name decision | The selected source name is retained. |
| Owner | ReadinessGapPlan |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ReadinessGapAnnotated |
| Protocols | Latest readiness and gap-plan decision seal |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ReadinessAnnotationView","firstHttpStatus":200,"terminalResultType":"ReadinessAnnotationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ReadinessGapPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Readiness/AnnotateReadinessGap.cs |
| Primary handler | AnnotateReadinessGapHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| GapId | Uuid | true |
| ExpectedRevision | long | true |
| Body | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AnnotateReadinessGapHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Readiness/PersonalReadinessAnnotationTransportTests.cs, test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### PlanReadinessGap

| Selected contract detail | Value |
| --- | --- |
| Target command name | PlanReadinessGap |
| Selected source command name | PlanReadinessGap |
| Name decision | The selected source name is retained. |
| Owner | ReadinessGapPlan |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ReadinessGapPlanned |
| Protocols | Latest readiness and gap-plan decision seal |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ReadinessGapPlanView","firstHttpStatus":200,"terminalResultType":"ReadinessGapPlanView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ReadinessGapPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Readiness/PlanReadinessGap.cs |
| Primary handler | PlanReadinessGapHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| GapId | Uuid | true |
| ExpectedRevision | long | true |
| OwnerMemberId | Uuid | true |
| TargetDate | DateOnly | true |
| Action | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan-readiness-gap/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| PlanReadinessGapHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AuthorizeReadinessGapPlanReadinessDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeReadinessGapPlanReadinessDecision |
| Selected source command name | AuthorizeReadinessGapPlanReadinessDecisionUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | ReadinessGapPlan |
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
| Primary handler | AuthorizeReadinessGapPlanReadinessDecisionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AuthorizeReadinessGapPlanReadinessDecisionUseSourceProof | true |
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

### CloseReadinessGapPlanReadinessDecisionAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseReadinessGapPlanReadinessDecisionAuthorization |
| Selected source command name | SettleReadinessGapPlanReadinessDecisionUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | ReadinessGapPlan |
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
| Primary handler | SettleReadinessGapPlanReadinessDecisionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleReadinessGapPlanReadinessDecisionUseSourceProof | true |
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
| AnnotateReadinessGap | AnnotateReadinessGapHandler | true | personal_http |
| PlanReadinessGap | PlanReadinessGapHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
