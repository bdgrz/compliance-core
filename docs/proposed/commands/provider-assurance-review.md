# ProviderAssuranceReview commands



Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/provider-assurance-review.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordProviderReview | RecordProviderReview | http | ReviewId | 4 | PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews/{review_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AbortProviderReview | AbortProviderReview | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompleteProviderAssuranceReview | CompleteProviderAssuranceReview | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### RecordProviderReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordProviderReview |
| Selected source command name | RecordProviderReview |
| Name decision | The selected source name is retained. |
| Target owner | ProviderAssuranceReview |
| Selected source owner | ProviderAssuranceReview |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ReviewId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ProviderReviewRequested |
| Protocols | Independent assurance facts and exact source proofs |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ReviewId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderReviewRegistration"}` |
| Expected revision scope | `{"owner":"ProviderAssuranceReview","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/RecordProviderReview.cs |
| Primary handler | RecordProviderReviewHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ReviewId | Uuid | true |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| Content | ProviderReviewContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews/{review_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordProviderReviewHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs, test/Compliance.Tests/Features/Providers/ProviderAssurancePipelineTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortProviderReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | AbortProviderReview |
| Selected source command name | AbortProviderReview |
| Name decision | The selected source name is retained. |
| Target owner | ProviderAssuranceReview |
| Selected source owner | ProviderAssuranceReview |
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

### CompleteProviderAssuranceReview

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteProviderAssuranceReview |
| Selected source command name | CompleteProviderAssuranceReview |
| Name decision | The selected source name is retained. |
| Target owner | ProviderAssuranceReview |
| Selected source owner | ProviderAssuranceReview |
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
| Primary handler | CompleteProviderAssuranceReviewHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ProviderReviewRequested> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ReviewId | Uuid | true |
| ReportUseProof | ImmutableContentRef | true |

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
| RecordProviderReview | RecordProviderReviewHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
