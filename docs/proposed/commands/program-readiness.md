# ProgramReadiness commands



Selected command contracts: **7**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/program-readiness.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| DecideReadiness | DecideReadiness | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| DecideTypeIEntry | DecideTypeIEntry | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RunReadinessAssessment | RunReadinessAssessment | http | AssessmentId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AbortReadinessDecision | AbortReadinessDecision | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| AdoptReadinessAssessment | AdoptReadinessAssessment | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| CompleteReadinessDecision | CompleteReadinessDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| CompleteTypeIEntryDecision | CompleteTypeIEntryDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### DecideReadiness

| Selected contract detail | Value |
| --- | --- |
| Target command name | DecideReadiness |
| Selected source command name | DecideReadiness |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ReadinessDecisionAccepted |
| Protocols | Latest readiness and gap-plan decision seal |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ReadinessDecisionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ProgramReadiness","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Readiness/DecideReadiness.cs |
| Primary handler | DecideReadinessHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DecideReadinessHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Readiness/PersonalReadinessClosureTransportTests.cs, test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### DecideTypeIEntry

| Selected contract detail | Value |
| --- | --- |
| Target command name | DecideTypeIEntry |
| Selected source command name | DecideTypeIEntry |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | TypeIEntryDecisionAccepted |
| Protocols | Latest readiness and gap-plan decision seal |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"TypeIEntryDecisionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ProgramReadiness","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Readiness/DecideTypeIEntry.cs |
| Primary handler | DecideTypeIEntryHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| AcknowledgedGapIds | IReadOnlyList<Uuid>? | false |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DecideTypeIEntryHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Programs/ProgramStageProjectionTests.cs, test/Compliance.Tests/Features/Readiness/PersonalTypeIEntryTransportTests.cs, test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs, test/Compliance.Tests/Features/Examinations/ExaminationEngagementCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RunReadinessAssessment

| Selected contract detail | Value |
| --- | --- |
| Target command name | RunReadinessAssessment |
| Selected source command name | RunReadinessAssessment |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | AssessmentId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ReadinessAssessmentRequested |
| Protocols | Latest readiness and gap-plan decision seal |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required AssessmentId., Persist complete fixed-as-of input snapshot and latest-adoption admission before child computation. At most one pending latest-adoption operation; source absence is recorded as a gap only after authoritative completeness, infrastructure failure remains transient. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ReadinessAssessmentRegistration"}` |
| Expected revision scope | `{"field":"ExpectedProgramRevision","owner":"ProgramReadiness","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Readiness/RunReadinessAssessment.cs |
| Primary handler | RunReadinessAssessmentHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| AssessmentId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExpectedProgramRevision | long | true |
| AsOf | DateTimeOffset? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RunReadinessAssessmentHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs, test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortReadinessDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | AbortReadinessDecision |
| Selected source command name | AbortReadinessDecision |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
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

### AdoptReadinessAssessment

| Selected contract detail | Value |
| --- | --- |
| Target command name | AdoptReadinessAssessment |
| Selected source command name | AdoptReadinessAssessment |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
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
| Primary handler | AdoptReadinessAssessmentHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ReadinessAssessmentRecorded> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AdmissionGeneration | long | true |
| RequestProof | SourceFactProof<ReadinessAssessmentRequested> | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteReadinessDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteReadinessDecision |
| Selected source command name | CompleteReadinessDecision |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
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
| Primary handler | CompleteReadinessDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ReadinessDecisionAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| AssessmentId | Uuid | true |
| PlanProofs | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteTypeIEntryDecision

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteTypeIEntryDecision |
| Selected source command name | CompleteTypeIEntryDecision |
| Name decision | The selected source name is retained. |
| Target owner | ProgramReadiness |
| Selected source owner | ProgramReadiness |
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
| Primary handler | CompleteTypeIEntryDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<TypeIEntryDecisionAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| AssessmentId | Uuid | true |
| PlanProofs | ImmutableContentRef | true |

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
| DecideReadiness | DecideReadinessHandler | true | personal_http |
| DecideTypeIEntry | DecideTypeIEntryHandler | true | personal_http |
| RunReadinessAssessment | RunReadinessAssessmentHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
