# PolicyDistributionCampaign commands



Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/policy-distribution-campaign.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CloseCampaign | CloseCampaign | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| LaunchPolicyCampaign | LaunchPolicyCampaign | http | CampaignId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns/{campaign_id} [ComplianceAuthorizationPolicies.ApiUser] |
| LaunchTrainingCampaign | LaunchTrainingCampaign | http | CampaignId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns/{campaign_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReconcileCampaignAudience | ReconcileCampaignAudience | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| AbortPolicyCampaignClosure | AbortPolicyCampaignClosure | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompletePolicyCampaignClosure | CompletePolicyCampaignClosure | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### CloseCampaign

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseCampaign |
| Selected source command name | CloseCampaign |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | PolicyCampaignClosureAccepted |
| Protocols | Participant admission and campaign closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"PolicyDistributionCampaign","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/CloseCampaign.cs |
| Primary handler | CloseCampaignHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| CampaignId | Uuid | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CloseCampaignHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### LaunchPolicyCampaign

| Selected contract detail | Value |
| --- | --- |
| Target command name | LaunchPolicyCampaign |
| Selected source command name | LaunchPolicyCampaign |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CampaignId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | PolicyCampaignLaunched |
| Protocols | Participant admission and campaign closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CampaignId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"CampaignRegistration","firstHttpStatus":200,"terminalResultType":"CampaignRegistration"}` |
| Expected revision scope | `{"owner":"PolicyDistributionCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/LaunchPolicyCampaign.cs |
| Primary handler | LaunchPolicyCampaignHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CampaignId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PolicyId | Uuid | true |
| PolicyVersion | long | true |
| RosterSnapshotId | Uuid | true |
| DueOn | DateOnly | true |
| Instructions | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns/{campaign_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| LaunchPolicyCampaignHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs, test/Compliance.Tests/Features/PolicyDistribution/PersonalCampaignWaiverTransportTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### LaunchTrainingCampaign

| Selected contract detail | Value |
| --- | --- |
| Target command name | LaunchTrainingCampaign |
| Selected source command name | LaunchTrainingCampaign |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CampaignId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | PolicyCampaignLaunched |
| Protocols | Participant admission and campaign closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CampaignId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"CampaignRegistration","firstHttpStatus":200,"terminalResultType":"CampaignRegistration"}` |
| Expected revision scope | `{"owner":"PolicyDistributionCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/LaunchTrainingCampaign.cs |
| Primary handler | LaunchTrainingCampaignHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CampaignId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequirementId | Uuid | true |
| RequirementVersion | long | true |
| RosterSnapshotId | Uuid | true |
| DueOn | DateOnly | true |
| Instructions | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns/{campaign_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| LaunchTrainingCampaignHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReconcileCampaignAudience

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReconcileCampaignAudience |
| Selected source command name | ReconcileCampaignAudience |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | PolicyCampaignAudienceAmended |
| Protocols | Participant admission and campaign closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"CampaignReconciliationView","firstHttpStatus":200,"terminalResultType":"CampaignReconciliationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"PolicyDistributionCampaign","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/ReconcileCampaignAudience.cs |
| Primary handler | ReconcileCampaignAudienceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| CampaignId | Uuid | true |
| RosterSnapshotId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReconcileCampaignAudienceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AbortPolicyCampaignClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | AbortPolicyCampaignClosure |
| Selected source command name | AbortPolicyCampaignClosure |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
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

### CompletePolicyCampaignClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompletePolicyCampaignClosure |
| Selected source command name | CompletePolicyCampaignClosure |
| Name decision | The selected source name is retained. |
| Target owner | PolicyDistributionCampaign |
| Selected source owner | PolicyDistributionCampaign |
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
| Primary handler | CompletePolicyCampaignClosureHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<PolicyCampaignClosureAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| CampaignId | Uuid | true |
| ParticipantProofManifest | ImmutableContentRef | true |

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
| AcknowledgePolicy | AcknowledgePolicyHandler | true | personal_http |
| ApproveCampaignWaiver | ApproveCampaignWaiverHandler | true | personal_http |
| CloseCampaign | CloseCampaignHandler | true | authorized_http_and_mcp |
| LaunchPolicyCampaign | LaunchPolicyCampaignHandler | true | authorized_http_and_mcp |
| LaunchTrainingCampaign | LaunchTrainingCampaignHandler | true | authorized_http_and_mcp |
| ReconcileCampaignAudience | ReconcileCampaignAudienceHandler | true | authorized_http_and_mcp |
| RecordTrainingCompletion | RecordTrainingCompletionHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
