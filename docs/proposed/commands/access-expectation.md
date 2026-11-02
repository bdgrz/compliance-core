# AccessExpectation commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-expectation.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| ApproveAccessExpectation | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ExemptAccessExpectation | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ProposeAccessExpectation | http | ExpectationId | 11 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ApproveAccessExpectation

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessExpectation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessExpectationApproved |
| Protocols | Expectation succession and population exceptions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessExpectationView","firstHttpStatus":200,"terminalResultType":"AccessExpectationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ApproveAccessExpectation.cs |
| Primary handler | ApproveAccessExpectationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectationId | Uuid | true |
| ExpectedRevision | long | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApproveAccessExpectationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs, test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ExemptAccessExpectation

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessExpectation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessExpectationExceptionRecorded |
| Protocols | Expectation succession and population exceptions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessExpectationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessExpectationExceptionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ExemptAccessExpectation.cs |
| Primary handler | ExemptAccessExpectationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectationId | Uuid | true |
| ExpectedRevision | long | true |
| ProviderSubjectId | string | true |
| Rationale | string | true |
| ExpiresAt | DateTimeOffset | true |
| ProviderEntitlementId | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ExemptAccessExpectationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ProposeAccessExpectation

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessExpectation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ExpectationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessExpectationProposed |
| Protocols | Expectation succession and population exceptions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ExpectationId., Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessExpectationView","firstHttpStatus":200,"terminalResultType":"AccessExpectationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ProposeAccessExpectation.cs |
| Primary handler | ProposeAccessExpectationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ExpectationId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedRevision | long | true |
| RuleKind | string | true |
| Parameters | AccessExpectationParameters | true |
| Rationale | string | true |
| EffectiveFrom | DateTimeOffset | true |
| EffectiveUntil | DateTimeOffset? | false |
| SupersedesExpectationId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ProposeAccessExpectationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs, test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ApproveAccessExpectation | ApproveAccessExpectationHandler | true | personal_http |
| ExemptAccessExpectation | ExemptAccessExpectationHandler | true | personal_http |
| ProposeAccessExpectation | ProposeAccessExpectationHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
