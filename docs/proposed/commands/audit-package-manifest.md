# AuditPackageManifest commands

Selected command contracts: **8**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/audit-package-manifest.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AmendAuditPackage | AmendAuditPackage | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/amendments/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |
| ApproveAuditPackageDisclosure | ApproveAuditPackageDisclosure | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-disclosure/{command_id} [ApiUser + existing request-scope authorizers] |
| ApproveAuditPackageValidationOverride | ApproveAuditPackageValidationOverride | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-validation-override/{command_id} [ApiUser + existing request-scope authorizers] |
| CreateAuditPackage | CreateAuditPackage | http | PackageId | 4 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id} [ComplianceAuthorizationPolicies.ApiUser] |
| RecordAuditPackageBuildResult | RecordAuditPackageBuildResult | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| ReportAuditPackageDelivery | RecordAuditPackageDelivery | http | DeliveryId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/deliveries/{delivery_id} |
| RequestAuditPackageBuild | RequestAuditPackageBuild | http | BuildKey | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/builds/{build_key} |
| ValidateAuditPackage | ValidateAuditPackage | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/validations/{command_id} [ComplianceAuthorizationPolicies.ApiUser] |

## Selected command details

### AmendAuditPackage

| Selected contract detail | Value |
| --- | --- |
| Target command name | AmendAuditPackage |
| Selected source command name | AmendAuditPackage |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditPackageAmended |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/AmendAuditPackage.cs |
| Primary handler | AmendAuditPackageHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| ExpectedRevision | long | true |
| AmendmentReason | string | true |
| IncludedRecordIds | IReadOnlyList<Uuid>? | false |
| Content | AuditPackageContent? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/amendments/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| AmendAuditPackageHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/AmendAuditPackageHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ApproveAuditPackageDisclosure

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveAuditPackageDisclosure |
| Selected source command name | ApproveAuditPackageDisclosure |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditPackageDisclosureApproved |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageDisclosure.cs |
| Primary handler | ApproveAuditPackageDisclosureHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| Recipient | string | true |
| Purpose | string | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-disclosure/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApproveAuditPackageDisclosureHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ApproveAuditPackageValidationOverride

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveAuditPackageValidationOverride |
| Selected source command name | ApproveAuditPackageValidationOverride |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditPackageValidationOverrideApproved |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageValidationOverride.cs |
| Primary handler | ApproveAuditPackageValidationOverrideHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| ValidationIssue | string | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-validation-override/{command_id} | ApiUser + existing request-scope authorizers |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ApproveAuditPackageValidationOverrideHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### CreateAuditPackage

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateAuditPackage |
| Selected source command name | CreateAuditPackage |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | PackageId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditPackageCreated |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | None listed |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":null,"owner":"AuditPackageManifest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/CreateAuditPackage.cs |
| Primary handler | CreateAuditPackageHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| Content | AuditPackageContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| CreateAuditPackageHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/CreateAuditPackageHandlerTests.cs, test/Compliance.Tests/Features/AuditPackages/CreateAuditPackageContractTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RecordAuditPackageBuildResult

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordAuditPackageBuildResult |
| Selected source command name | RecordAuditPackageBuildResult |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; source_event_effect; UUIDv5(source eventId, length-prefixed effectKind + canonical target stream + source lifecycle episode); external process callback source is the immutable accepted business source event plus exact effect key, never attemptId. |
| Entry point | internal_trusted_command |
| MCP | false |
| Selected semantic events | AuditPackageBuilt |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event., Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":"Explicit null requires exact source authorization/generation and physical append OCC; never substitute current logical revision."}` |
| Authorizer sources | src/Compliance.Core/Features/AuditPackages/RecordAuditPackageBuildResultAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/RecordAuditPackageBuildResult.cs |
| Primary handler | RecordAuditPackageBuildResultHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<AuditPackageBuildRequested> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| PackageId | Uuid | true |
| BuildId | Uuid | true |
| Output | AuditPackageBuildOutput | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAuditPackageBuildResultHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/RecordAuditPackageBuildResultHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ReportAuditPackageDelivery

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReportAuditPackageDelivery |
| Selected source command name | RecordAuditPackageDelivery |
| Name decision | Names the person's action to report when and to whom the selected audit package was delivered. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | DeliveryId; Uuid; required=true; client; Must equal existing UUIDv5(53cb20d6-01ef-5fd5-99b3-04087e39bf77, packageId + newline + packageVersion + newline + Clean(recipient) + newline + Clean(deliveryReference)); this identifies exact manual delivery recording. |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AuditPackageDeliveryAdmitted |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Retain personal HTTP actor and authored DeliveredAt/DeliveryReference. Verify deterministic DeliveryId and current exact archive/manifest/validation/recipient approval before admission. Freeze delivered version at this admission. Child AuditPackageDelivery materializes the accepted record; no automated send or provider callback is introduced. |
| Guard order | bind_required_ids, authenticate_personal_http_and_authorize, normalize_and_lookup_original_intent, compare_original_binding, check_expected_manifest_revision_and_current_delivery_gate, freeze_complete_PackageDeliveryRecordingIntent, append_AuditPackageDeliveryAdmitted |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedManifestRevision","owner":"AuditPackageManifest","meaning":"current package manifest owner revision at personal recording admission"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/RecordAuditPackageDelivery.cs |
| Primary handler | RecordAuditPackageDeliveryHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| Recipient | string | true |
| DeliveredAt | DateTimeOffset | true |
| DeliveryReference | string | true |
| DeliveryId | Uuid | true |
| ExpectedManifestRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/deliveries/{delivery_id} | [Placeholder: value not recorded] |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RecordAuditPackageDeliveryHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### RequestAuditPackageBuild

| Selected contract detail | Value |
| --- | --- |
| Target command name | RequestAuditPackageBuild |
| Selected source command name | RequestAuditPackageBuild |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | BuildKey; string; required=true; client; Required BuildKey with PackageId/PackageVersion. IIdentifiedRequest.RequestId = UUIDv5(698c0bb6-2dfc-58b3-b2fd-2f3dc04369f8, $"{PackageId}\n{PackageVersion}\n{BuildKey.Trim()}"); preserve existing token validation. |
| Entry point | personal_http |
| MCP | false |
| Selected semantic events | AuditPackageBuildRequested |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required owner-local ExpectedManifestRevision; business Version remains an exact content version, not owner OCC., Freeze BuildId derived by the existing BuildNamespace algorithm, exact manifest version/reference/selection and original actor in PackageBuildIntent before starting worker. Technical AttemptId is a separate process field. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedManifestRevision","owner":"AuditPackageManifest","meaning":"revision of the public first-admission owner; never a new child owner revision"}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/RequestAuditPackageBuild.cs |
| Primary handler | RequestAuditPackageBuildHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ExaminationId | Uuid | true |
| PackageId | Uuid | true |
| PackageKind | string | true |
| BuildKey | string | true |
| ExpectedManifestRevision | long | true |
| PackageVersion | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/builds/{build_key} | [Placeholder: value not recorded] |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| RequestAuditPackageBuildHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome

### ValidateAuditPackage

| Selected contract detail | Value |
| --- | --- |
| Target command name | ValidateAuditPackage |
| Selected source command name | ValidateAuditPackage |
| Name decision | The selected source name is retained. |
| Owner | AuditPackageManifest |
| Contract record status | target_contract |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; client; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | authorized_http |
| MCP | false |
| Selected semantic events | AuditPackageValidated |
| Protocols | Manifest validation, immutable build and recipient delivery |
| Policy preservation | All current source handler/aggregate validation rules and source test expectations apply, except the explicit contractChanges and boundary protocol. Lifetime caps and technical attempt events are removed with their structural replacement. |
| Contract changes | Add required CommandId., Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. |
| Guard order | bind_required_ids, authenticate_authorize, canonicalize_authored_intent, resolve_native_operation_facts, compare_owner_episode_actor_kind_intent_original_revision, validate_new_owner_revision_and_source_permits, append_complete_concrete_attributed_batch |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}` |
| Authorizer sources | src/Compliance.Core/Features/AccessControl/ClientManagementMutationAuthorizer.cs, src/Compliance.Core/Features/Programs/ProgramManagementAuthorizer.cs |
| Source contract | src/Compliance.Common/Features/AuditPackages/ValidateAuditPackage.cs |
| Primary handler | ValidateAuditPackageHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| ValidationKey | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/validations/{command_id} | ComplianceAuthorizationPolicies.ApiUser |

#### Handler source evidence

| Handler | Registered | Source |
| --- | --- | --- |
| ValidateAuditPackageHandler | true | [Placeholder: source file is not present in this checkout] |

#### Test evidence recorded by the target contract

Existing tests: test/Compliance.Tests/Features/AuditPackages/ValidateAuditPackageHandlerTests.cs

Required tests: ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange, ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision, ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity, ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof, ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome


## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| AmendAuditPackage | AmendAuditPackageHandler | true | authorized_http |
| ApproveAuditPackageDisclosure | ApproveAuditPackageDisclosureHandler | true | authorized_http |
| ApproveAuditPackageValidationOverride | ApproveAuditPackageValidationOverrideHandler | true | authorized_http |
| CreateAuditPackage | CreateAuditPackageHandler | true | authorized_http |
| RecordAuditPackageBuildResult | RecordAuditPackageBuildResultHandler | true | internal_trusted_command |
| RecordAuditPackageDelivery | RecordAuditPackageDeliveryHandler | true | personal_http |
| RequestAuditPackageBuild | RequestAuditPackageBuildHandler | true | personal_http |
| ValidateAuditPackage | ValidateAuditPackageHandler | true | authorized_http |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
