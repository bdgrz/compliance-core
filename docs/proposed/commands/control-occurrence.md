# ControlOccurrence commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-occurrence.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AttestControlOccurrence | AttestControlOccurrence | http | CommandId | 14 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CorrectControlAttestation | CorrectControlAttestation | http | CommandId | 15 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| OpenControlOccurrence | OpenControlOccurrence | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviewControlOccurrence | ReviewControlOccurrence | http | CommandId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| OpenDueControlOccurrence | MaterializeControlOccurrence | receiver | CommandId | 11 | [Placeholder: value not recorded] |

## Selected command details

### AttestControlOccurrence

| Selected contract detail | Value |
| --- | --- |
| Target command name | AttestControlOccurrence |
| Selected source command name | AttestControlOccurrence |
| Name decision | The selected source name is retained. |
| Owner | ControlOccurrence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlOccurrenceAttested |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/AttestControlOccurrence.cs |
| Primary handler | AttestControlOccurrenceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| ExpectedRevision | long | true |
| Result | string | true |
| PerformedAt | DateTimeOffset | true |
| CoveredFrom | DateOnly? | true |
| CoveredUntil | DateOnly? | true |
| Notes | string? | true |
| Rationale | string? | true |
| Evidence | IReadOnlyList<EvidenceReference> | true |
| PerformedByPersonId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AttestControlOccurrenceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs, test/Compliance.Tests/Features/Work/ConcurrentOccurrenceSubmissionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CorrectControlAttestation

| Selected contract detail | Value |
| --- | --- |
| Target command name | CorrectControlAttestation |
| Selected source command name | CorrectControlAttestation |
| Name decision | The selected source name is retained. |
| Owner | ControlOccurrence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlOccurrenceAttested |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/CorrectControlAttestation.cs |
| Primary handler | CorrectControlAttestationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| ExpectedRevision | long | true |
| Result | string | true |
| PerformedAt | DateTimeOffset | true |
| CoveredFrom | DateOnly? | true |
| CoveredUntil | DateOnly? | true |
| Notes | string? | true |
| Rationale | string? | true |
| Evidence | IReadOnlyList<EvidenceReference> | true |
| CorrectionReason | string | true |
| PerformedByPersonId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CorrectControlAttestationHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Operations/PersonalOccurrenceProofTransportTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs, test/Compliance.Tests/Features/Operations/ControlOccurrenceTests.cs, test/Compliance.Tests/Features/Work/FitzControlOccurrenceWorkItemDirectoryTests.cs, test/Compliance.Tests/Features/Work/NotApplicableOccurrenceReviewTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### OpenControlOccurrence

| Selected contract detail | Value |
| --- | --- |
| Target command name | OpenControlOccurrence |
| Selected source command name | OpenControlOccurrence |
| Name decision | The selected source name is retained. |
| Owner | ControlOccurrence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | ControlOccurrenceOpened |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC., Resolve OccurrenceId deterministically from tenant/program/control/BusinessWindow across all generators. CommandId distinguishes authored opening attempts; trigger and date cannot create a second occurrence for the same window. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/OpenControlOccurrence.cs |
| Primary handler | OpenControlOccurrenceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| Trigger | string | true |
| OccurredOn | DateOnly | true |
| ExpectedRevision | long | true |
| CommandId | Uuid | true |
| BusinessWindow | ControlBusinessWindow | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| OpenControlOccurrenceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs, test/Compliance.Tests/Features/Operations/ControlOccurrenceTests.cs, test/Compliance.Tests/Features/Work/FitzControlOccurrenceWorkItemDirectoryTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/NotApplicableOccurrenceReviewTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviewControlOccurrence

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewControlOccurrence |
| Selected source command name | ReviewControlOccurrence |
| Name decision | The selected source name is retained. |
| Owner | ControlOccurrence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ControlOccurrenceReviewed |
| Protocols | Operating plan and business-window occurrence |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Operations/ReviewControlOccurrence.cs |
| Primary handler | ReviewControlOccurrenceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| ExpectedRevision | long | true |
| AttestationId | Uuid | true |
| Outcome | string | true |
| Rationale | string | true |
| RequestedActions | IReadOnlyList<string>? | false |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviewControlOccurrenceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### OpenDueControlOccurrence

| Selected contract detail | Value |
| --- | --- |
| Target command name | OpenDueControlOccurrence |
| Selected source command name | MaterializeControlOccurrence |
| Name decision | Opens the due control work item for its exact period, plan, owner, and reviewer. |
| Owner | ControlOccurrence |
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
| Primary handler | MaterializeControlOccurrenceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<DueControlOccurrencesGenerated> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| BusinessWindow | ControlBusinessWindow | true |
| OccurrenceId | Uuid | true |

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
| AttestControlOccurrence | AttestControlOccurrenceHandler | true | personal_http |
| CorrectControlAttestation | CorrectControlAttestationHandler | true | personal_http |
| OpenControlOccurrence | OpenControlOccurrenceHandler | true | authorized_http |
| ReviewControlOccurrence | ReviewControlOccurrenceHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
