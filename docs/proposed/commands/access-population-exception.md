# AccessPopulationException commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-population-exception.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ExemptMissingAccessPopulation | ExemptMissingAccessPopulation | http | ExceptionId | 7 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ExemptMissingAccessPopulation

| Selected contract detail | Value |
| --- | --- |
| Target command name | ExemptMissingAccessPopulation |
| Selected source command name | ExemptMissingAccessPopulation |
| Name decision | The selected source name is retained. |
| Owner | AccessPopulationException |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ExceptionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessPopulationExceptionRecorded |
| Protocols | Expectation succession and population exceptions |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ExceptionId., Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessPopulationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessPopulationExceptionView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessPopulationException","meaning":"new independent owner creation requires zero; former directory/tenant/system ledger revision is not reused"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ExemptMissingAccessPopulation.cs |
| Primary handler | ExemptMissingAccessPopulationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ExceptionId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |
| ExpiresAt | DateTimeOffset | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ExemptMissingAccessPopulationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ExemptMissingAccessPopulation | ExemptMissingAccessPopulationHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
