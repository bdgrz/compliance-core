# ClientServiceIndependence commands

Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/client-service-independence.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| EvaluateClientIndependence | http | EvaluationId | 5 | PUT /api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id} [ApiUser + existing request-scope authorizers] |
| RecordNonattestService | http | ServiceRecordId | 4 | PUT /api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id} [ApiUser + existing request-scope authorizers] |
| AcquireEngagementServiceEpoch | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| ReleaseEngagementServiceEpoch | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| AuthorizeClientServicePersonalDecisionUse | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| SettleClientServicePersonalDecisionUse | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### EvaluateClientIndependence

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | EvaluationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | ClientIndependenceEvaluationAdmitted |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Freeze complete as-of service/practice/rule input snapshot on source admission. Child computes pure historical preview and always keeps ProductionAcceptanceBlocked=true. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"IndependenceEvaluationView"}` |
| Expected revision scope | `{"field":"ExpectedServiceEpoch","owner":"ClientServiceIndependence","meaning":"current complete service history epoch, never new preview child revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/EvaluateClientIndependence.cs |
| Primary handler | EvaluateClientIndependenceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EvaluationId | Uuid | true |
| RuleSetVersion | long | true |
| ExaminationPeriodStart | DateOnly | true |
| ExpectedServiceEpoch | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| EvaluateClientIndependenceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/IndependenceOperationTests.cs, test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordNonattestService

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | ServiceRecordId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http_and_mcp |
| MCP | true |
| Selected semantic events | NonattestServiceRecorded |
| Protocols | Service epoch and permanent practice commitments |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | None listed |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"NonattestServiceView","firstHttpStatus":200,"terminalResultType":"NonattestServiceView"}` |
| Expected revision scope | `{"field":"ExpectedServiceEpoch","owner":"ClientServiceIndependence","meaning":"complete service history epoch only; evaluation/read-use records do not advance it"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/AccessControl/IndependenceAdministrationAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AccessControl/RecordNonattestService.cs |
| Primary handler | RecordNonattestServiceHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ServiceRecordId | Uuid | true |
| ExpectedServiceEpoch | long | true |
| Content | NonattestServiceContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordNonattestServiceHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs, test/Compliance.Tests/Features/AccessControl/SourceIndependenceReevaluationTests.cs, test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### AcquireEngagementServiceEpoch

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
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
| Primary handler | AcquireEngagementServiceEpochHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | AcquireEngagementServiceEpochSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| ServiceEpoch | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReleaseEngagementServiceEpoch

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
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
| Primary handler | ReleaseEngagementServiceEpochHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | ReleaseEngagementServiceEpochSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| ServiceEpoch | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeClientServicePersonalDecisionUse

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
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
| Primary handler | AuthorizeClientServicePersonalDecisionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<EngagementPersonalDecisionAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |
| ServiceEpoch | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### SettleClientServicePersonalDecisionUse

| Selected contract detail | Value |
| --- | --- |
| Owner | ClientServiceIndependence |
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
| Primary handler | SettleClientServicePersonalDecisionUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleClientServicePersonalDecisionUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| EngagementId | Uuid | true |

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
| EvaluateClientIndependence | EvaluateClientIndependenceHandler | true | authorized_http_and_mcp |
| RecordNonattestService | RecordNonattestServiceHandler | true | authorized_http_and_mcp |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
