# ApplicationImportBatch commands

Selected command contracts: **8**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/application-import-batch.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AcceptApplicationImport | AcceptApplicationImport | http | CommandId | 4 | PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| CancelApplicationImport | CancelApplicationImport | http | CommandId | 5 | PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordApplicationImportRowChoice | CorrelateApplicationImportRow | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ExecuteApplicationImport | ExecuteApplicationImport | command | CommandId | 4 | [Placeholder: value not recorded] |
| FreezeApplicationImportOmissionProposal | FreezeApplicationImportOmissionProposal | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| StageApplicationImport | StageApplicationImport | http | SubmissionId | 6 | PUT /api/v1/tenants/{tenant_id}/application-imports/{submission_id} |
| CommitApplicationImportBatch | CommitApplicationImportBatch | receiver | CommandId | 9 | [Placeholder: value not recorded] |
| CompleteApplicationImportCancellation | CompleteApplicationImportCancellation | receiver | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AcceptApplicationImport

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcceptApplicationImport |
| Selected source command name | AcceptApplicationImport |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ApplicationImportPlanAccepted |
| Protocols | Complete batch application and publication |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/AcceptApplicationImport.cs |
| Primary handler | AcceptApplicationImportHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AcceptApplicationImportHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CancelApplicationImport

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelApplicationImport |
| Selected source command name | CancelApplicationImport |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ApplicationImportCancellationAccepted |
| Protocols | Complete batch application and publication |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/CancelApplicationImport.cs |
| Primary handler | CancelApplicationImportHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CancelApplicationImportHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportLifecycleHandlerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordApplicationImportRowChoice

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordApplicationImportRowChoice |
| Selected source command name | CorrelateApplicationImportRow |
| Name decision | Records the person's choice to link the row to an existing application or create a new one. |
| Owner | ApplicationImportBatch |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ApplicationImportRowChoiceRecorded |
| Protocols | Complete batch application and publication |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/CorrelateApplicationImportRow.cs |
| Primary handler | CorrelateApplicationImportRowHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| RowId | Uuid | true |
| ExpectedRevision | long | true |
| Decision | string | true |
| ApplicationId | Uuid? | true |
| ExpectedApplicationRevision | long? | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CorrelateApplicationImportRowHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportCommitTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportMissingPreviewTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportFrozenPlanTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportCorrelationTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportRetirementPlanTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportAcceptancePlanTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportSourceClaimTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportReconciliationPreviewTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportPreviewCorrelationTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportReadTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportPendingEffectTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportRetirementEffectTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportLifecycleHandlerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportEffectVerificationTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ExecuteApplicationImport

| Selected contract detail | Value |
| --- | --- |
| Target command name | ExecuteApplicationImport |
| Selected source command name | ExecuteApplicationImport |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
| Contract record status | target_contract |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; source_event_effect; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | internal_trusted_command |
| MCP | false |
| Selected semantic events | ApplicationImportExecutionAuthorized |
| Protocols | Complete batch application and publication |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"ApplicationImportBatch","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/Applications/ExecuteApplicationImportAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/ExecuteApplicationImport.cs |
| Primary handler | ExecuteApplicationImportHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ExecuteApplicationImportHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs, test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### FreezeApplicationImportOmissionProposal

| Selected contract detail | Value |
| --- | --- |
| Target command name | FreezeApplicationImportOmissionProposal |
| Selected source command name | FreezeApplicationImportOmissionProposal |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | ApplicationImportRetirementProposalFinalized |
| Protocols | Complete batch application and publication |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Applications/ApplicationInventoryAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/Applications/FreezeApplicationImportOmissionProposal.cs |
| Primary handler | FreezeApplicationImportOmissionProposalHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| ExpectedRevision | long | true |
| ExpectedSourcePosition | ulong | true |
| ExpectedContentSha256 | string | true |
| Reason | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| FreezeApplicationImportOmissionProposalHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### StageApplicationImport

| Selected contract detail | Value |
| --- | --- |
| Target command name | StageApplicationImport |
| Selected source command name | StageApplicationImport |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | SubmissionId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"ApplicationImportRegistration","firstHttpStatus":200,"terminalResultType":"ApplicationImportRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubmissionId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| Rows | IReadOnlyList<ApplicationImportInputRow> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/application-imports/{submission_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CommitApplicationImportBatch

| Selected contract detail | Value |
| --- | --- |
| Target command name | CommitApplicationImportBatch |
| Selected source command name | CommitApplicationImportBatch |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
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
| Primary handler | CommitApplicationImportBatchHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ApplicationImportExecutionAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| TargetProofManifest | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CompleteApplicationImportCancellation

| Selected contract detail | Value |
| --- | --- |
| Target command name | CompleteApplicationImportCancellation |
| Selected source command name | CompleteApplicationImportCancellation |
| Name decision | The selected source name is retained. |
| Owner | ApplicationImportBatch |
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
| Primary handler | CompleteApplicationImportCancellationHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<ApplicationImportCancellationAccepted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| BatchId | Uuid | true |
| TargetFenceManifest | ImmutableContentRef | true |

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
| AcceptApplicationImport | AcceptApplicationImportHandler | true | personal_http |
| CancelApplicationImport | CancelApplicationImportHandler | true | personal_http |
| CorrelateApplicationImportRow | CorrelateApplicationImportRowHandler | true | personal_http |
| ExecuteApplicationImport | ExecuteApplicationImportHandler | true | internal_trusted_command |
| FreezeApplicationImportOmissionProposal | FreezeApplicationImportOmissionProposalHandler | true | personal_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
