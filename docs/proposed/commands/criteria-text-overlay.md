# CriteriaTextOverlay commands

Selected command contracts: **1**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/criteria-text-overlay.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| SetCriteriaTextOverlay | SetCriteriaTextOverlay | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id} [ApiUser + existing request-scope authorizers] |

## Selected command details

### SetCriteriaTextOverlay

| Selected contract detail | Value |
| --- | --- |
| Target command name | SetCriteriaTextOverlay |
| Selected source command name | SetCriteriaTextOverlay |
| Name decision | The selected source name is retained. |
| Owner | CriteriaTextOverlay |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | CriteriaTextOverlayEntryRevised |
| Protocols | Per-criterion overlay revision |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"CriteriaTextOverlayRegistration","firstHttpStatus":200,"terminalResultType":"CriteriaTextOverlayRegistration"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"CriteriaTextOverlay","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Criteria/SetCriteriaTextOverlay.cs |
| Primary handler | SetCriteriaTextOverlayHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| EditionId | Uuid | true |
| Identifier | string | true |
| ExpectedRevision | long | true |
| Content | CriteriaTextOverlayContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| SetCriteriaTextOverlayHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs, test/Compliance.Tests/Features/Criteria/CriteriaTextOverlayAuthorizationTests.cs, test/Compliance.Tests/Features/Criteria/SetCriteriaTextOverlayHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| SetCriteriaTextOverlay | SetCriteriaTextOverlayHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
