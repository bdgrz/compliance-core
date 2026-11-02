# ProviderAssuranceGap commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/provider-assurance-gap.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| CloseProviderCoverageGap | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/close-provider-coverage-gap/{command_id} [ApiUser + existing request-scope authorizers] |
| LinkProviderCoverageGapRiskAcceptance | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{command_id} [ApiUser + existing request-scope authorizers] |
| RecordProviderCoverageGap | http | GapId | 4 | PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### CloseProviderCoverageGap

| Selected contract detail | Value |
| --- | --- |
| Owner | ProviderAssuranceGap |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ProviderCoverageGapClosed |
| Protocols | Independent assurance facts and exact source proofs |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ProviderCoverageGapRegistration","firstHttpStatus":200,"terminalResultType":"ProviderCoverageGapRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ProviderAssuranceGap","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/CloseProviderCoverageGap.cs |
| Primary handler | CloseProviderCoverageGapHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| GapId | Uuid | true |
| ExpectedRevision | long | true |
| Content | ProviderCoverageGapClosureContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/close-provider-coverage-gap/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CloseProviderCoverageGapHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### LinkProviderCoverageGapRiskAcceptance

| Selected contract detail | Value |
| --- | --- |
| Owner | ProviderAssuranceGap |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ProviderCoverageGapRiskAcceptanceAdmitted |
| Protocols | Independent assurance facts and exact source proofs |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Open-gap and duplicate (RiskId, AcceptanceId) checks linearize on gap admission. Child LinkId is this CommandId; materialization preserves admission time even if gap subsequently closes or acceptance later expires. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderCoverageGapRegistration"}` |
| Expected revision scope | `{"field":"ExpectedGapRevision","owner":"ProviderAssuranceGap","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/LinkProviderCoverageGapRiskAcceptance.cs |
| Primary handler | LinkProviderCoverageGapRiskAcceptanceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ProviderId | Uuid | true |
| GapId | Uuid | true |
| ExpectedGapRevision | long | true |
| RiskId | Uuid | true |
| AcceptanceId | Uuid | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| LinkProviderCoverageGapRiskAcceptanceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordProviderCoverageGap

| Selected contract detail | Value |
| --- | --- |
| Owner | ProviderAssuranceGap |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | GapId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ProviderCoverageGapRecorded |
| Protocols | Independent assurance facts and exact source proofs |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required GapId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ProviderCoverageGapRegistration","firstHttpStatus":200,"terminalResultType":"ProviderCoverageGapRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"ProviderAssuranceGap","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Providers/RecordProviderCoverageGap.cs |
| Primary handler | RecordProviderCoverageGapHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| GapId | Uuid | true |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| Content | ProviderCoverageGapContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordProviderCoverageGapHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| CloseProviderCoverageGap | CloseProviderCoverageGapHandler | true | authorized_http |
| LinkProviderCoverageGapRiskAcceptance | LinkProviderCoverageGapRiskAcceptanceHandler | true | authorized_http |
| RecordProviderCoverageGap | RecordProviderCoverageGapHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
