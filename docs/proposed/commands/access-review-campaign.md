# AccessReviewCampaign commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-review-campaign.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| CompleteAccessReviewCampaign | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| LaunchAccessReviewCampaign | http | CampaignId | 8 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordBulkAccessDecision | http | DecisionBatchId | 8 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions/{decision_batch_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AbortAccessReviewCampaignCompletion | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompleteAccessReviewCampaignClosure | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### CompleteAccessReviewCampaign

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessReviewCampaignCompletionAccepted |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"AccessReviewCampaignCompletionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewCampaign","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/CompleteAccessReviewCampaign.cs |
| Primary handler | CompleteAccessReviewCampaignHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ExpectedRevision | long | true |
| Attestation | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CompleteAccessReviewCampaignHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs, test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/RestrictedAccessReviewCampaignReadTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### LaunchAccessReviewCampaign

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CampaignId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessReviewCampaignLaunched |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CampaignId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessReviewCampaignRegistration","firstHttpStatus":200,"terminalResultType":"AccessReviewCampaignRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"AccessReviewCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/LaunchAccessReviewCampaign.cs |
| Primary handler | LaunchAccessReviewCampaignHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CampaignId | Uuid | true |
| TenantId | Uuid | true |
| Name | string | true |
| Instructions | string | true |
| Deadline | DateTimeOffset | true |
| Assignments | IReadOnlyList<AccessReviewAssignment> | true |
| ProgramId | Uuid? | false |
| RemediationOwnerMemberId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| LaunchAccessReviewCampaignHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewInfrastructureTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordBulkAccessDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | DecisionBatchId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessReviewDecisionBatchAdmitted |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required DecisionBatchId., Add required owner-local ExpectedCampaignRevision; business Version remains an exact content version, not owner OCC., Admit complete batch intent on the campaign before child creation, so completion observes every admitted bulk operation. Preview binds exact selected item heads; child batch starts at revision zero. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"BulkAccessDecisionResult"}` |
| Expected revision scope | `{"field":"ExpectedCampaignRevision","owner":"AccessReviewCampaign","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/RecordBulkAccessDecision.cs |
| Primary handler | RecordBulkAccessDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemIds | IReadOnlyList<Uuid> | true |
| Decision | string | true |
| Rationale | string | true |
| PreviewToken | string | true |
| ExpectedCampaignRevision | long | true |
| DecisionBatchId | Uuid | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions/{decision_batch_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordBulkAccessDecisionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortAccessReviewCampaignCompletion

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewCampaign |
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

### CompleteAccessReviewCampaignClosure

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewCampaign |
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
| Primary handler | CompleteAccessReviewCampaignClosureHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewCampaignCompletionAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| ItemProofs | ImmutableContentRef | true |

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
| CompleteAccessReviewCampaign | CompleteAccessReviewCampaignHandler | true | personal_http |
| ExemptAccessRemediation | ExemptAccessRemediationHandler | true | personal_http |
| LaunchAccessReviewCampaign | LaunchAccessReviewCampaignHandler | true | authorized_http_and_mcp |
| ReassignAccessReviewResponsibility | ReassignAccessReviewResponsibilityHandler | true | authorized_http_and_mcp |
| RecordAccessDecision | RecordAccessDecisionHandler | true | personal_http |
| RecordAccessRemediationChange | RecordAccessRemediationChangeHandler | true | authorized_http_and_mcp |
| RecordBulkAccessDecision | RecordBulkAccessDecisionHandler | true | personal_http |
| VerifyAccessRemediation | VerifyAccessRemediationHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
