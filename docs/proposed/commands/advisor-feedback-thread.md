# AdvisorFeedbackThread commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/advisor-feedback-thread.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| CorrectAdvisorFeedback | http | CorrectionId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections/{correction_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ResolveAdvisorFeedback | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RespondToAdvisorFeedback | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| MaterializeAdvisorFeedback | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### CorrectAdvisorFeedback

| Selected contract detail | Value |
| --- | --- |
| Owner | AdvisorFeedbackThread |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CorrectionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AdvisorFeedbackCorrected |
| Protocols | Feedback admission versus handoff revocation |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AdvisorCollaboration/CorrectAdvisorFeedback.cs |
| Primary handler | CorrectAdvisorFeedbackHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| HandoffId | Uuid | true |
| FeedbackId | Uuid | true |
| CorrectionId | Uuid | true |
| CorrectedBody | string | true |
| CorrectionReason | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections/{correction_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CorrectAdvisorFeedbackHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Readiness/AdvisoryReadinessNoteIndependenceTests.cs, test/Compliance.Tests/Features/AdvisorCollaboration/CorrectAdvisorFeedbackHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ResolveAdvisorFeedback

| Selected contract detail | Value |
| --- | --- |
| Owner | AdvisorFeedbackThread |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AdvisorFeedbackResolved |
| Protocols | Feedback admission versus handoff revocation |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AdvisorCollaboration/ResolveAdvisorFeedback.cs |
| Primary handler | ResolveAdvisorFeedbackHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| HandoffId | Uuid | true |
| FeedbackId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ResolveAdvisorFeedbackHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RespondToAdvisorFeedback

| Selected contract detail | Value |
| --- | --- |
| Owner | AdvisorFeedbackThread |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AdvisorFeedbackResponded |
| Protocols | Feedback admission versus handoff revocation |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AdvisorCollaboration/RespondToAdvisorFeedback.cs |
| Primary handler | RespondToAdvisorFeedbackHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| HandoffId | Uuid | true |
| FeedbackId | Uuid | true |
| Response | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RespondToAdvisorFeedbackHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### MaterializeAdvisorFeedback

| Selected contract detail | Value |
| --- | --- |
| Owner | AdvisorFeedbackThread |
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
| Primary handler | MaterializeAdvisorFeedbackHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AdvisorFeedbackAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| HandoffId | Uuid | true |
| FeedbackId | Uuid | true |

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
| CorrectAdvisorFeedback | CorrectAdvisorFeedbackHandler | true | authorized_http |
| ResolveAdvisorFeedback | ResolveAdvisorFeedbackHandler | true | authorized_http |
| RespondToAdvisorFeedback | RespondToAdvisorFeedbackHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
