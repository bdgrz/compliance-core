# ArtifactLegalHold commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/artifact-legal-hold.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| PlaceArtifactLegalHold | PlaceArtifactLegalHold | http | HoldId | 7 | PUT /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReleaseArtifactLegalHold | ReleaseArtifactLegalHold | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### PlaceArtifactLegalHold

| Selected contract detail | Value |
| --- | --- |
| Target command name | PlaceArtifactLegalHold |
| Selected source command name | PlaceArtifactLegalHold |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactLegalHold |
| Selected source owner | ArtifactLegalHold |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | HoldId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ArtifactLegalHoldPlaced |
| Protocols | Retention basis and authoritative legal-hold barrier |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | None listed |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ArtifactLegalHold","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Retention/ArtifactRetentionAdminAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Retention/PlaceArtifactLegalHold.cs |
| Primary handler | PlaceArtifactLegalHoldHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SourceKind | string | true |
| SourceId | Uuid | true |
| ExpectedContentSha256 | string | true |
| HoldId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| PlaceArtifactLegalHoldHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs, test/Compliance.Tests/Features/Retention/AttestRetentionManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReleaseArtifactLegalHold

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReleaseArtifactLegalHold |
| Selected source command name | ReleaseArtifactLegalHold |
| Name decision | The selected source name is retained. |
| Target owner | ArtifactLegalHold |
| Selected source owner | ArtifactLegalHold |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ArtifactLegalHoldReleased |
| Protocols | Retention basis and authoritative legal-hold barrier |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ArtifactLegalHold","counter":"logical ResourceRevision of this owner"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Retention/ArtifactRetentionAdminAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Retention/ReleaseArtifactLegalHold.cs |
| Primary handler | ReleaseArtifactLegalHoldHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| SourceKind | string | true |
| SourceId | Uuid | true |
| ExpectedContentSha256 | string | true |
| HoldId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReleaseArtifactLegalHoldHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs, test/Compliance.Tests/Features/Retention/AttestRetentionManagementWriteWallTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| PlaceArtifactLegalHold | PlaceArtifactLegalHoldHandler | true | authorized_http |
| ReleaseArtifactLegalHold | ReleaseArtifactLegalHoldHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
