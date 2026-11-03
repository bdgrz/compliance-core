# FindingAggregate commands

Selected command contracts: **10**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/finding-aggregate.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AddCorrectiveAction | AddCorrectiveAction | http | ActionId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CloseFinding | CloseFinding | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| LinkFindingAcceptance | LinkFindingAcceptance | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RaiseFinding | RaiseFinding | http | FindingId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RaiseOccurrenceFinding | RaiseOccurrenceFinding | creation | FindingId | 8 | [Placeholder: value not recorded] |
| ReopenFinding | ReopenFinding | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ReviseFinding | ReviseFinding | http | CommandId | 11 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RaiseEvaluationDeviationFinding | RaiseEvaluationDeviationFinding | command | CommandId | 7 | [Placeholder: value not recorded] |
| AbortFindingClosure | AbortFindingClosure | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| CompleteFindingClosure | CompleteFindingClosure | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AddCorrectiveAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | AddCorrectiveAction |
| Selected source command name | AddCorrectiveAction |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ActionId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | CorrectiveActionAdmitted |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required ActionId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedFindingRevision","owner":"FindingAggregate","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/AddCorrectiveAction.cs |
| Primary handler | AddCorrectiveActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ActionId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedFindingRevision | long | true |
| Description | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AddCorrectiveActionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Work/WorkTestData.cs, test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CloseFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseFinding |
| Selected source command name | CloseFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | FindingClosureAccepted |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/CloseFinding.cs |
| Primary handler | CloseFindingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedRevision | long | true |
| VerificationRationale | string | true |
| ResolutionEvidence | IReadOnlyList<EvidenceReference> | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CloseFindingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs, test/Compliance.Tests/Features/Readiness/PersonalReadinessClosureTransportTests.cs, test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### LinkFindingAcceptance

| Selected contract detail | Value |
| --- | --- |
| Target command name | LinkFindingAcceptance |
| Selected source command name | LinkFindingAcceptance |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | FindingAcceptanceLinked |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/LinkFindingAcceptance.cs |
| Primary handler | LinkFindingAcceptanceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedRevision | long | true |
| Kind | string | true |
| RecordId | Uuid | true |
| DecisionId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| LinkFindingAcceptanceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RaiseFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | RaiseFinding |
| Selected source command name | RaiseFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | FindingId; Uuid; required=true; client; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | FindingRaised |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required FindingId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"FindingRegistration","firstHttpStatus":200,"terminalResultType":"FindingRegistration"}` |
| Expected revision scope | `{"field":null,"owner":"FindingAggregate","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/RaiseFinding.cs |
| Primary handler | RaiseFindingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| FindingId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Source | FindingSource | true |
| Title | string | true |
| Description | string | true |
| Severity | string | true |
| AffectedScope | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| Links | IReadOnlyList<FindingLink>? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RaiseFindingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs, test/Compliance.Tests/Features/Operations/OperationsFixture.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RaiseOccurrenceFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | RaiseOccurrenceFinding |
| Selected source command name | RaiseOccurrenceFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | creation |
| Logical identity | FindingId; Uuid; required=true; source_event_effect; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | internal_trusted_command |
| MCP | false |
| Selected semantic events | FindingRaised, CorrectiveActionAdmitted |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | When source kind is occurrence_review, preserve the default requested corrective action by atomically admitting its complete deterministic ActionId/content alongside FindingRaisedV2; materialize it independently. Failed/skipped attestation variants without requested action emit FindingRaisedV2 only. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"FindingAggregate","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/Remediation/OperationsReactionAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/RaiseOccurrenceFinding.cs |
| Primary handler | RaiseOccurrenceFindingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| SourceVersion | string | true |
| SourceKind | string | true |
| SourceText | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RaiseOccurrenceFindingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Remediation/OccurrenceFindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReopenFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReopenFinding |
| Selected source command name | ReopenFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | FindingReopened |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/ReopenFinding.cs |
| Primary handler | ReopenFindingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReopenFindingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReviseFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseFinding |
| Selected source command name | ReviseFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | FindingRevised |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/ReviseFinding.cs |
| Primary handler | ReviseFindingHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedRevision | long | true |
| Severity | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| AffectedScope | string | true |
| RootCause | string? | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ReviseFindingHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs, test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RaiseEvaluationDeviationFinding

| Selected contract detail | Value |
| --- | --- |
| Target command name | RaiseEvaluationDeviationFinding |
| Selected source command name | RaiseEvaluationDeviationFinding |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| DeviationId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AbortFindingClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | AbortFindingClosure |
| Selected source command name | AbortFindingClosure |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
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

### CompleteFindingClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteFindingClosure |
| Selected source command name | CompleteFindingClosure |
| Name decision | The selected source name is retained. |
| Owner | FindingAggregate |
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
| Primary handler | CompleteFindingClosureHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<FindingClosureAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| FindingId | Uuid | true |
| ActionProofManifest | ImmutableContentRef | true |

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
| AddCorrectiveAction | AddCorrectiveActionHandler | true | authorized_http |
| CloseFinding | CloseFindingHandler | true | personal_http |
| CompleteCorrectiveAction | CompleteCorrectiveActionHandler | true | authorized_http |
| LinkFindingAcceptance | LinkFindingAcceptanceHandler | true | authorized_http |
| RaiseFinding | RaiseFindingHandler | true | authorized_http_and_mcp |
| RaiseOccurrenceFinding | RaiseOccurrenceFindingHandler | true | internal_trusted_command |
| ReopenFinding | ReopenFindingHandler | true | authorized_http |
| ReviseFinding | ReviseFindingHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
