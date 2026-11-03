# TrainingRequirement commands

Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/training-requirement.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| DefineTrainingRequirement | DefineTrainingRequirement | http | RequirementId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseTrainingRequirement | ReviseTrainingRequirement | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/revise-training-requirement/{command_id} [ApiUser + existing request-scope authorizers] |

## Selected command details

### DefineTrainingRequirement

| Selected contract detail | Value |
| --- | --- |
| Target command name | DefineTrainingRequirement |
| Selected source command name | DefineTrainingRequirement |
| Name decision | The selected source name is retained. |
| Owner | TrainingRequirement |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | RequirementId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | TrainingRequirementDefined |
| Protocols | Independent training requirement |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required RequirementId., Supplied RequirementId must equal existing TrainingCatalog.IdFor(TenantId, ProgramId, Identifier); keep NormalizeIdentifier and its exact UUIDv5 algorithm unchanged. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"TrainingRequirementRegistration","firstHttpStatus":200,"terminalResultType":"TrainingRequirementRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"TrainingRequirement","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/DefineTrainingRequirement.cs |
| Primary handler | DefineTrainingRequirementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| RequirementId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Identifier | string | true |
| Content | TrainingRequirementContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| DefineTrainingRequirementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseTrainingRequirement

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseTrainingRequirement |
| Selected source command name | ReviseTrainingRequirement |
| Name decision | The selected source name is retained. |
| Owner | TrainingRequirement |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | TrainingRequirementDefined |
| Protocols | Independent training requirement |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Keep ExpectedVersion as the sole logical concurrency counter; do not require a redundant ExpectedRevision with the same meaning. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"TrainingRequirementRegistration","firstHttpStatus":200,"terminalResultType":"TrainingRequirementRegistration"}` |
| Expected revision scope | `{"field":"ExpectedVersion","owner":"TrainingRequirement","meaning":"this owner has one immutable defined content version per business revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/PolicyDistribution/ReviseTrainingRequirement.cs |
| Primary handler | ReviseTrainingRequirementHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequirementId | Uuid | true |
| ExpectedVersion | long | true |
| Content | TrainingRequirementContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/revise-training-requirement/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseTrainingRequirementHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| DefineTrainingRequirement | DefineTrainingRequirementHandler | true | authorized_http_and_mcp |
| ReviseTrainingRequirement | ReviseTrainingRequirementHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
