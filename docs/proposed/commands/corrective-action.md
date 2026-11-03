# CorrectiveAction commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/corrective-action.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| CompleteCorrectiveAction | CompleteCorrectiveAction | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CreateCorrectiveAction | MaterializeCorrectiveAction | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| AuthorizeCorrectiveActionFindingClosure | AuthorizeCorrectiveActionFindingClosureUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CloseCorrectiveActionFindingClosureAuthorization | SettleCorrectiveActionFindingClosureUse | receiver | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### CompleteCorrectiveAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteCorrectiveAction |
| Selected source command name | CompleteCorrectiveAction |
| Name decision | The selected source name is retained. |
| Owner | CorrectiveAction |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | CorrectiveActionCompleted |
| Protocols | Action admission and finding closure |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"CorrectiveAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Remediation/CompleteCorrectiveAction.cs |
| Primary handler | CompleteCorrectiveActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| ExpectedRevision | long | true |
| ActionId | Uuid | true |
| ResolutionNotes | string | true |
| Evidence | IReadOnlyList<EvidenceReference> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CompleteCorrectiveActionHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs, test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs, test/Compliance.Tests/Features/Work/WorkQueueTests.cs, test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs, test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs, test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs, test/Compliance.Tests/Features/Remediation/FindingTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CreateCorrectiveAction

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateCorrectiveAction |
| Selected source command name | MaterializeCorrectiveAction |
| Name decision | Creates accepted corrective work with its own lifecycle, even if the finding later closes. |
| Owner | CorrectiveAction |
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
| Primary handler | MaterializeCorrectiveActionHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<CorrectiveActionAdmitted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| FindingId | Uuid | true |
| ActionId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AuthorizeCorrectiveActionFindingClosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | AuthorizeCorrectiveActionFindingClosure |
| Selected source command name | AuthorizeCorrectiveActionFindingClosureUse |
| Name decision | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| Owner | CorrectiveAction |
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
| Primary handler | AuthorizeCorrectiveActionFindingClosureUseHandler |

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
| SourceOwnerId | Uuid | true |
| SourceRevision | long | true |
| ReceivingOwnerId | Uuid | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CloseCorrectiveActionFindingClosureAuthorization

| Selected contract detail | Value |
| --- | --- |
| Target command name | CloseCorrectiveActionFindingClosureAuthorization |
| Selected source command name | SettleCorrectiveActionFindingClosureUse |
| Name decision | The command ends the exact authorization after its business action or cancellation. |
| Owner | CorrectiveAction |
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
| Primary handler | SettleCorrectiveActionFindingClosureUseHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SettleCorrectiveActionFindingClosureUseSourceProof | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| SourceOwnerId | Uuid | true |
| ReceivingOwnerId | Uuid | true |
| Disposition | SourceUseSettlementDisposition | true |

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
| CompleteCorrectiveAction | CompleteCorrectiveActionHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
