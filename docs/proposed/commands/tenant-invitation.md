# TenantInvitation commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/tenant-invitation.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| AcceptTenantInvitation | http | InvitationId | 4 | PUT /api/v1/tenants/{tenant_id}/invitations/{invitation_id}/acceptance [ComplianceAuthorizationPolicies.ApiUser] |
| InviteGuestConsultant | http | InvitationId | 5 | PUT /api/v1/tenants/{tenant_id}/guest-invitations/{invitation_id} [ComplianceAuthorizationPolicies.ApiUser] |
| InviteOrganizationMember | http | InvitationId | 4 | PUT /api/v1/tenants/{tenant_id}/member-invitations/{invitation_id} [ComplianceAuthorizationPolicies.ApiUser] |
| InviteTenantMember | http | InvitationId | 5 | PUT /api/v1/tenants/{tenant_id}/invitations/{invitation_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### AcceptTenantInvitation

| Selected contract detail | Value |
| --- | --- |
| Owner | TenantInvitation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | TenantInvitationAccepted |
| Protocols | Mailbox generation, invitation and delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | InvitationId is route-bound and token verification must resolve the same immutable invitation; token is a secret verification input and is excluded from persisted authored intent. Persist verified token/challenge provenance without bearer material., Add required InvitationId., No ExpectedRevision: acceptance consumes a one-time token, the invitee holds only the token link, and the invitation lifecycle (pending, accepted, expired, canceled) already serializes acceptance. This matches CompleteIdentityRecovery; both are token-consumption commands. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"TenantInvitation","counter":"none; token consumption serializes on the invitation lifecycle","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/Tenants/AcceptTenantInvitationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Tenants/AcceptTenantInvitation.cs |
| Primary handler | AcceptTenantInvitationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| InvitationId | Uuid | true |
| TenantId | Uuid | true |
| EmailAddress | string | true |
| Token | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/invitations/{invitation_id}/acceptance | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AcceptTenantInvitationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### InviteGuestConsultant

| Selected contract detail | Value |
| --- | --- |
| Owner | TenantInvitation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | TenantMemberInvited |
| Protocols | Mailbox generation, invitation and delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required InvitationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientRbacMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Tenants/InviteGuestConsultant.cs |
| Primary handler | InviteGuestConsultantHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| InvitationId | Uuid | true |
| TenantId | Uuid | true |
| EmailAddress | string | true |
| ProgramIds | IReadOnlyList<Uuid> | true |
| ExpiresOn | DateOnly | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/guest-invitations/{invitation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| InviteGuestConsultantHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/RbacManagementAuthorizerTests.cs, test/Compliance.Tests/Features/Tenants/InviteGuestConsultantHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### InviteOrganizationMember

| Selected contract detail | Value |
| --- | --- |
| Owner | TenantInvitation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | TenantMemberInvited |
| Protocols | Mailbox generation, invitation and delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required InvitationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientRbacMutationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Tenants/InviteOrganizationMember.cs |
| Primary handler | InviteOrganizationMemberHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| InvitationId | Uuid | true |
| TenantId | Uuid | true |
| EmailAddress | string | true |
| BuiltInRole | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/member-invitations/{invitation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| InviteOrganizationMemberHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### InviteTenantMember

| Selected contract detail | Value |
| --- | --- |
| Owner | TenantInvitation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | TenantMemberInvited |
| Protocols | Mailbox generation, invitation and delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required InvitationId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/Tenants/PlatformOperatorAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Tenants/InviteTenantMember.cs |
| Primary handler | InviteTenantMemberHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| InvitationId | Uuid | true |
| TenantId | Uuid | true |
| EmailAddress | string | true |
| Affiliation | string | true |
| Administrator | bool | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/invitations/{invitation_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| InviteTenantMemberHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: None listed

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| AcceptTenantInvitation | AcceptTenantInvitationHandler | true | authorized_http |
| InviteGuestConsultant | InviteGuestConsultantHandler | true | authorized_http_and_mcp |
| InviteOrganizationMember | InviteOrganizationMemberHandler | true | authorized_http_and_mcp |
| InviteTenantMember | InviteTenantMemberHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
