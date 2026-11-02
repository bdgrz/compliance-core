# AccessPrincipalClassification commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-principal-classification.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| ClassifyAccessPrincipal | http | CommandId | 11 | PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### ClassifyAccessPrincipal

| Selected contract detail | Value |
| --- | --- |
| Owner | AccessPrincipalClassification |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessPrincipalClassified |
| Protocols | Population acceptance and principal classification |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedClassificationCount to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessPrincipalClassificationView","firstHttpStatus":200,"terminalResultType":"AccessPrincipalClassificationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessPrincipalClassification","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/ClassifyAccessPrincipal.cs |
| Primary handler | ClassifyAccessPrincipalHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| ProviderSubjectId | string | true |
| ExpectedRevision | long | true |
| Classification | string | true |
| Rationale | string | true |
| PersonId | Uuid? | false |
| ServiceIdentityId | Uuid? | false |
| AccountableOwnerPersonId | Uuid? | false |
| SharedJustification | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ClassifyAccessPrincipalHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs, test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| ClassifyAccessPrincipal | ClassifyAccessPrincipalHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
