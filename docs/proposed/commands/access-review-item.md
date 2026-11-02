# AccessReviewItem commands

Selected command contracts: **12**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-review-item.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| ExemptAccessRemediation | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReassignAccessReviewResponsibility | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordAccessDecision | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordAccessRemediationChange | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| VerifyAccessRemediation | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| MaterializeAccessReviewItem | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| ReserveAccessReviewItemDecision | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| ApplyAccessReviewItemDecision | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| FenceAccessReviewItemDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| AuthorizeAccessReviewItemCampaignCompletionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| SettleAccessReviewItemCampaignCompletionUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| SettleAccessReviewItemDecision | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### ExemptAccessRemediation

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessRemediationExceptionRecorded |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessRemediationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessRemediationExceptionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ExemptAccessRemediation.cs |
| Primary handler | ExemptAccessRemediationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |
| ExpiresAt | DateTimeOffset? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ExemptAccessRemediationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs, test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReassignAccessReviewResponsibility

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessReviewResponsibilityReassigned |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessReviewResponsibilityReassignmentView","firstHttpStatus":200,"terminalResultType":"AccessReviewResponsibilityReassignmentView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ReassignAccessReviewResponsibility.cs |
| Primary handler | ReassignAccessReviewResponsibilityHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| Responsibility | string | true |
| ExpectedRevision | long | true |
| AssignedMemberId | Uuid | true |
| Reason | string | true |
| DelegationReason | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReassignAccessReviewResponsibilityHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs, test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAccessDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessDecisionsRecorded |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessDecisionView","firstHttpStatus":200,"terminalResultType":"AccessDecisionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/RecordAccessDecision.cs |
| Primary handler | RecordAccessDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| ExpectedRevision | long | true |
| Decision | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAccessDecisionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs, test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/RestrictedAccessReviewCampaignReadTests.cs, test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs, test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAccessRemediationChange

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessRemediationChangeRecorded |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessRemediationChangeView","firstHttpStatus":200,"terminalResultType":"AccessRemediationChangeView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/RecordAccessRemediationChange.cs |
| Primary handler | RecordAccessRemediationChangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| ExpectedRevision | long | true |
| Reference | string | true |
| Description | string | true |
| ChangedAt | DateTimeOffset | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAccessRemediationChangeHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs, test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### VerifyAccessRemediation

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessRemediationVerified |
| Protocols | Campaign item and atomic bulk decision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessRemediationVerificationView","firstHttpStatus":200,"terminalResultType":"AccessRemediationVerificationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/VerifyAccessRemediation.cs |
| Primary handler | VerifyAccessRemediationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| ExpectedRevision | long | true |
| PopulationId | Uuid | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| VerifyAccessRemediationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs, test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### MaterializeAccessReviewItem

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | MaterializeAccessReviewItemHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewCampaignLaunched> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReserveAccessReviewItemDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | ReserveAccessReviewItemDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| DecisionBatchId | Uuid | true |
| SourceRevision | ulong | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyAccessReviewItemDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | ApplyAccessReviewItemDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| ItemId | Uuid | true |
| DecisionBatchId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### FenceAccessReviewItemDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | FenceAccessReviewItemDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AccessReviewDecisionBatchCancellationAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| DecisionBatchId | Uuid | true |
| ItemId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeAccessReviewItemCampaignCompletionUse

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | AuthorizeAccessReviewItemCampaignCompletionUseHandler |

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
| SourceOwnerId | Uuid | true |
| SourceRevision | long | true |
| ReceivingOwnerId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SettleAccessReviewItemCampaignCompletionUse

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | SettleAccessReviewItemCampaignCompletionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleAccessReviewItemCampaignCompletionUseSourceProof | true |
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

### SettleAccessReviewItemDecision

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessReviewItem |
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
| Primary handler | SettleAccessReviewItemDecisionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleAccessReviewItemDecisionSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| DecisionBatchId | Uuid | true |
| ItemId | Uuid | true |

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
| ExemptAccessRemediation | ExemptAccessRemediationHandler | true | personal_http |
| ReassignAccessReviewResponsibility | ReassignAccessReviewResponsibilityHandler | true | authorized_http_and_mcp |
| RecordAccessDecision | RecordAccessDecisionHandler | true | personal_http |
| RecordAccessRemediationChange | RecordAccessRemediationChangeHandler | true | authorized_http_and_mcp |
| VerifyAccessRemediation | VerifyAccessRemediationHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
