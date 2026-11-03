# ApplicationRelationship commands

Selected command contracts: **3**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/application-relationship.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AddApplicationRelationship | AddApplicationRelationship | http | RelationshipId | 6 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id} |
| ApproveApplicationSuccessor | ApproveApplicationSuccessor | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/applications/{predecessor_application_id}/successors/{successor_application_id}/approval/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RemoveApplicationRelationship | RemoveApplicationRelationship | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}/removals/{command_id} |

## Selected command details

### AddApplicationRelationship

| Selected contract detail | Value |
| --- | --- |
| Target command name | AddApplicationRelationship |
| Selected source command name | AddApplicationRelationship |
| Name decision | The selected source name is retained. |
| Owner | ApplicationRelationship |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | RelationshipId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ApplicationRelationshipAdded |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision., Canonical sole public relationship creation command. Preserve active endpoint/visibility and source/target revision guards from the removed RecordApplicationRelationship path; RelationshipId must equal exact existing tuple IdFor. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ApplicationRelationshipChange","firstHttpStatus":200,"terminalResultType":"ApplicationRelationshipChange"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/AddApplicationRelationship.cs |
| Primary handler | AddApplicationRelationshipHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| RelationshipId | Uuid | true |
| ExpectedRevision | long | true |
| TargetApplicationId | Uuid | true |
| RelationshipType | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id} | null (recorded) |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AddApplicationRelationshipHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRelationshipTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ApproveApplicationSuccessor

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveApplicationSuccessor |
| Selected source command name | ApproveApplicationSuccessor |
| Name decision | The selected source name is retained. |
| Owner | ApplicationRelationship |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ApplicationRelationshipApproved |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/ApproveApplicationSuccessor.cs |
| Primary handler | ApproveApplicationSuccessorHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| PredecessorApplicationId | Uuid | true |
| SuccessorApplicationId | Uuid | true |
| ExpectedSourceRevision | long | true |
| ExpectedTargetRevision | long | true |
| ExpectedRevision | long | true |
| ImpactDigest | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{predecessor_application_id}/successors/{successor_application_id}/approval/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApproveApplicationSuccessorHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationSuccessorApprovalTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs, test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RemoveApplicationRelationship

| Selected contract detail | Value |
| --- | --- |
| Target command name | RemoveApplicationRelationship |
| Selected source command name | RemoveApplicationRelationship |
| Name decision | The selected source name is retained. |
| Owner | ApplicationRelationship |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ApplicationRelationshipRemoved |
| Protocols | Application identity, relationships and import holds |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision., Canonical sole relationship removal command; preserve all endpoint/type/authority and reason rules from removed governed-removal path. Require Reason. Use owner-local ExpectedRevision, not a directory visible revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ApplicationRelationshipChange","firstHttpStatus":200,"terminalResultType":"ApplicationRelationshipChange"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/RemoveApplicationRelationship.cs |
| Primary handler | RemoveApplicationRelationshipHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| RelationshipId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}/removals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RemoveApplicationRelationshipHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs, test/Compliance.Tests/Features/Applications/ApplicationRelationshipTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| AddApplicationRelationship | AddApplicationRelationshipHandler | true | authorized_http |
| ApproveApplicationSuccessor | ApproveApplicationSuccessorHandler | true | personal_http |
| RemoveApplicationRelationship | RemoveApplicationRelationshipHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
