# AccessPopulation commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/access-population.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AcceptAccessPopulation | AcceptAccessPopulation | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| OpenAccessPopulation | OpenAccessPopulation | http | PopulationId | 7 | PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordAccessPopulationFacts | RecordAccessPopulationFacts | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/access-populations/record-access-population-facts/{command_id} [ApiUser + existing request-scope authorizers] |

## Selected command details

### AcceptAccessPopulation

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcceptAccessPopulation |
| Selected source command name | AcceptAccessPopulation |
| Name decision | The selected source name is retained. |
| Target owner | AccessPopulation |
| Selected source owner | AccessPopulation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AccessPopulationAccepted |
| Protocols | Population acceptance and principal classification |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessPopulationAcceptance","firstHttpStatus":200,"terminalResultType":"AccessPopulationAcceptance"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessPopulation","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/AcceptAccessPopulation.cs |
| Primary handler | AcceptAccessPopulationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| ExpectedRevision | long | true |
| Attestation | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AcceptAccessPopulationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs, test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### OpenAccessPopulation

| Selected contract detail | Value |
| --- | --- |
| Target command name | OpenAccessPopulation |
| Selected source command name | OpenAccessPopulation |
| Name decision | The selected source name is retained. |
| Target owner | AccessPopulation |
| Selected source owner | AccessPopulation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | PopulationId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessPopulationOpened |
| Protocols | Population acceptance and principal classification |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required PopulationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessPopulationRegistration","firstHttpStatus":200,"terminalResultType":"AccessPopulationRegistration"}` |
| Expected revision scope | `{"owner":"AccessPopulation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/OpenAccessPopulation.cs |
| Primary handler | OpenAccessPopulationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| PopulationId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| ExpectedSystemInstanceRevision | long | true |
| ObservedAt | DateTimeOffset | true |
| Source | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-populations/{population_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| OpenAccessPopulationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs, test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAccessPopulationFacts

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAccessPopulationFacts |
| Selected source command name | RecordAccessPopulationFacts |
| Name decision | The selected source name is retained. |
| Target owner | AccessPopulation |
| Selected source owner | AccessPopulation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | AccessPopulationFactsRecorded |
| Protocols | Population acceptance and principal classification |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"AccessPopulationRegistration","firstHttpStatus":200,"terminalResultType":"AccessPopulationRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AccessPopulation","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessReviews/AccessReviewAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessReviews/RecordAccessPopulationFacts.cs |
| Primary handler | RecordAccessPopulationFactsHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| ExpectedRevision | long | true |
| Principals | IReadOnlyList<AccessPrincipalFact> | true |
| Entitlements | IReadOnlyList<AccessEntitlementFact> | true |
| GroupMembers | IReadOnlyList<AccessGroupMemberFact> | true |
| Assignments | IReadOnlyList<AccessAssignmentFact> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/access-populations/record-access-population-facts/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAccessPopulationFactsHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs, test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs, test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| AcceptAccessPopulation | AcceptAccessPopulationHandler | true | personal_http |
| ClassifyAccessPrincipal | ClassifyAccessPrincipalHandler | true | authorized_http_and_mcp |
| OpenAccessPopulation | OpenAccessPopulationHandler | true | authorized_http_and_mcp |
| RecordAccessPopulationFacts | RecordAccessPopulationFactsHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
