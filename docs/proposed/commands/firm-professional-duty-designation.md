# FirmProfessionalDutyDesignation commands

Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/firm-professional-duty-designation.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| RecordFirmProfessionalDutyDesignation | http | AssertionId | 4 | PUT /api/v1/platform/firm-professional-duties/{designation_id}/assertions/{assertion_id} [existing platform firm-duty authorizer + signed assertion verifier] |
| RevokeFirmProfessionalDutyDesignation | http | AssertionId | 4 | PUT /api/v1/platform/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id} [existing platform firm-duty authorizer + signed assertion verifier] |
| RejectProfessionalDutyChange | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| ApplyProfessionalDutyChange | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| AuthorizeFirmProfessionalDutyDesignationEngagementUse | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| SettleFirmProfessionalDutyDesignationEngagementUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### RecordFirmProfessionalDutyDesignation

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | AssertionId; Uuid; required=true; client; one immutable signed authority assertion; verified AssertionId must match the signed assertion |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ProfessionalDutyChangeAccepted |
| Protocols | Signed assertion claims and duty-scope designation |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision., AssertionId is required and must equal the verified signed assertion identity; DesignationId cannot identify repeated refresh/revocation assertions. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FirmProfessionalDutyDesignationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FirmProfessionalDutyDesignation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ProfessionalDutyAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/RecordFirmProfessionalDutyDesignation.cs |
| Primary handler | RecordFirmProfessionalDutyDesignationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| AssertionId | Uuid | true |
| DesignationId | Uuid | true |
| SignedAssertion | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/platform/firm-professional-duties/{designation_id}/assertions/{assertion_id} | existing platform firm-duty authorizer + signed assertion verifier |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordFirmProfessionalDutyDesignationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/ProfessionalDutyGovernanceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RevokeFirmProfessionalDutyDesignation

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | AssertionId; Uuid; required=true; client; one immutable signed authority assertion; verified AssertionId must match the signed assertion |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ProfessionalDutyChangeAccepted |
| Protocols | Signed assertion claims and duty-scope designation |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision., AssertionId is required and must equal the verified signed assertion identity; DesignationId cannot identify repeated refresh/revocation assertions. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FirmProfessionalDutyDesignationView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FirmProfessionalDutyDesignation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ProfessionalDutyAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/RevokeFirmProfessionalDutyDesignation.cs |
| Primary handler | RevokeFirmProfessionalDutyDesignationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| AssertionId | Uuid | true |
| DesignationId | Uuid | true |
| SignedAssertion | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/platform/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id} | existing platform firm-duty authorizer + signed assertion verifier |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RevokeFirmProfessionalDutyDesignationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/ProfessionalDutyGovernanceCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RejectProfessionalDutyChange

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | abort |
| Logical identity | CommandId; Uuid; required=true; abort receiver identity bound to the exact admitted operation and generation: exact redelivery resolves the original rejected result; an abort after a terminal outcome is a no-op |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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

### ApplyProfessionalDutyChange

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | ApplyProfessionalDutyChangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ProfessionalAssertionIdentityClaimed> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| DesignationId | Uuid | true |
| AssertionId | Uuid | true |
| StaffEligibilityUse | SourceFactProof<FirmStaffProfessionalDutyUseAuthorized>? | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeFirmProfessionalDutyDesignationEngagementUse

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | AuthorizeFirmProfessionalDutyDesignationEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AuthorizeFirmProfessionalDutyDesignationEngagementUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| SourceContentEventId | Uuid | true |
| ExpectedContentRevision | ulong | true |
| EngagementId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SettleFirmProfessionalDutyDesignationEngagementUse

| Selected contract detail | Value |
| --- | --- |
| Owner | FirmProfessionalDutyDesignation |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | SettleFirmProfessionalDutyDesignationEngagementUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleFirmProfessionalDutyDesignationEngagementUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| EngagementId | Uuid | true |
| Disposition | SourceUseSettlementDisposition | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

| Command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| RecordFirmProfessionalDutyDesignation | RecordFirmProfessionalDutyDesignationHandler | true | authorized_http |
| RevokeFirmProfessionalDutyDesignation | RevokeFirmProfessionalDutyDesignationHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
