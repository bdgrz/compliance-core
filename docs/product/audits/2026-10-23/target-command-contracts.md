# Target command implementation contracts

Selected target signatures for 161 source commands referenced by 29 split roots; three obsolete commands are removed. Additional entries appear in [source admission](source-admission-contracts.md) and [remaining commands](remaining-command-identities.md). These signatures replace source transport layouts. [Owner protocols](owner-protocols.md), [native acceptance](native-acceptance-contract.md), [typed reactor contracts](target-reactor-contracts.md) and [revision/response rules](revision-and-response-contracts.md) complete the handlers.

Required route values fill identical typed properties; conflicting body duplicates reject. Preserve source personal restrictions and policies. Async write replay always repeats original 202; status/result GETs expose completion. The machine companion retains exact source policy paths and required regression names.

## AcceptAccessPopulation

- **Handler → first owner:** `AcceptAccessPopulationHandler` → `AccessPopulation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `PopulationId: Uuid`, `ExpectedRevision: long`, `Attestation: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessPopulation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessPopulationAccepted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessPopulationAcceptance","firstHttpStatus":200,"terminalResultType":"AccessPopulationAcceptance"}`.
- **Owner protocols:** Population acceptance and principal classification.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/AcceptAccessPopulation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/AcceptAccessPopulation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs`, `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`.

## AcceptApplicationImport

- **Handler → first owner:** `AcceptApplicationImportHandler` → `ApplicationImportBatch`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `BatchId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationImportPlanSealed`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Complete batch application and publication.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/AcceptApplicationImport.cs](../../../../src/Compliance.Common/Features/Applications/Commands/AcceptApplicationImport.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs`.

## AcceptServiceEngagement

- **Handler → first owner:** `AcceptServiceEngagementHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementAcceptanceRequested`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ServiceEngagementAcceptanceView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/AcceptServiceEngagement.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/AcceptServiceEngagement.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs`.

## AcceptTenantInvitation

- **Handler → first owner:** `AcceptTenantInvitationHandler` → `TenantInvitation`.
- **Logical identity:** `{"field":"InvitationId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/invitations/{invitation_id}/acceptance`; MCP disabled.
- **Exact fields:** `InvitationId: Uuid`, `TenantId: Uuid`, `EmailAddress: string`, `Token: string`.
- **Revision scope:** `{"field":null,"owner":"TenantInvitation","counter":"none; token consumption serializes on the invitation lifecycle","nullableInternalRule":null}`.
- **Native facts:** `TenantInvitationAccepted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Mailbox generation, invitation and delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/Tenants/AcceptTenantInvitation.cs](../../../../src/Compliance.Common/Features/Tenants/Commands/AcceptTenantInvitation.cs).
- **Selected changes:** InvitationId is route-bound and token verification must resolve the same immutable invitation; token is a secret verification input and is excluded from persisted authored intent. Persist verified token/challenge provenance without bearer material. Add required InvitationId. No ExpectedRevision: acceptance consumes a one-time token, the invitee holds only the token link, and the invitation lifecycle already serializes acceptance (as CompleteIdentityRecovery does).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs`.

## AcknowledgeEngagementManagement

- **Handler → first owner:** `AcknowledgeEngagementManagementHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"AcknowledgementId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `EngagementId: Uuid`, `AcknowledgementId: Uuid`, `ExpectedEngagementRevision: long`, `CompleteServiceRecordIds: IReadOnlyList<Uuid>`, `Statement: string`, `ExpectedServiceEpoch: long`.
- **Revision scope:** `{"field":"ExpectedEngagementRevision","owner":"ServiceEngagementIndependence","meaning":"current engagement content revision; ExpectedServiceEpoch separately binds complete client history"}`.
- **Native facts:** `EngagementPersonalDecisionAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"EngagementManagementAcknowledgementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/AcknowledgeEngagementManagement.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/AcknowledgeEngagementManagement.cs).
- **Selected changes:** Drop obsolete tenant ExpectedSequence/ExpectedRevision. Admit personal decision against ExpectedEngagementRevision and ExpectedServiceEpoch, pin engagement content, then materialize after exact source uses. Personal records remain independently owned; admission alone does not approve production acceptance.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Testing/AttestAssignmentHistoryFixture.cs`, `test/Compliance.Tests/Features/AccessControl/PartnerIndependenceEvaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/IndependenceAssignmentBoundaryTests.cs`, `test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/ActualStaffEngagementLocatorTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/EngagementManagementAcknowledgementHandlerTests.cs`, `test/Compliance.Tests/Features/AccessControl/EngagementManagementAcknowledgementTests.cs`, `test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs`, `test/Compliance.Tests/Features/AccessControl/SourceIndependenceReevaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## AcknowledgePolicy

- **Handler → first owner:** `AcknowledgePolicyHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `CampaignId: Uuid`, `PersonId: Uuid`, `PolicyVersion: long`, `ContentSha256: string`, `AcknowledgementText: string`, `ExpectedCampaignRevision: long`.
- **Revision scope:** `{"field":"ExpectedCampaignRevision","owner":"PolicyDistributionCampaign","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `PolicyParticipantResultAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignAcknowledgementView"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/AcknowledgePolicy.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Queries/AcknowledgePolicy.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedCampaignRevision; business Version remains an exact content version, not owner OCC. Admit complete participant result on the open campaign generation first; apply to participant through committed admission fact. Personal source actor is retained.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/TrainingWorkAuthorityTests.cs`.

## AddApplicationRelationship

- **Handler → first owner:** `AddApplicationRelationshipHandler` → `ApplicationRelationship`.
- **Logical identity:** `{"field":"RelationshipId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ApplicationId: Uuid`, `RelationshipId: Uuid`, `ExpectedRevision: long`, `TargetApplicationId: Uuid`, `RelationshipType: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationRelationshipAdded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ApplicationRelationshipChange","firstHttpStatus":200,"terminalResultType":"ApplicationRelationshipChange"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/Commands/AddApplicationRelationship.cs](../../../../src/Compliance.Common/Features/Applications/Commands/AddApplicationRelationship.cs).
- **Selected changes:** Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision. Canonical sole public relationship creation command. Preserve active endpoint/visibility and source/target revision guards from the removed RecordApplicationRelationship path; RelationshipId must equal exact existing tuple IdFor.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRelationshipTests.cs`.

## AddCorrectiveAction

- **Handler → first owner:** `AddCorrectiveActionHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"ActionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}`; MCP disabled.
- **Exact fields:** `ActionId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedFindingRevision: long`, `Description: string`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`.
- **Revision scope:** `{"field":"ExpectedFindingRevision","owner":"FindingAggregate","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `CorrectiveActionAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/AddCorrectiveAction.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/AddCorrectiveAction.cs).
- **Selected changes:** Add required ActionId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/WorkTestData.cs`, `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## AddRiskTreatmentAction

- **Handler → first owner:** `AddRiskTreatmentActionHandler` → `RiskTreatmentAction`.
- **Logical identity:** `{"field":"ActionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}`; MCP disabled.
- **Exact fields:** `ActionId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ExpectedRevision: long`, `Title: string`, `TargetState: string`, `ExpectedEvidence: string`, `DueOn: DateOnly`, `AccountableMemberId: Uuid`, `EvidenceRequestIds: IReadOnlyList<Uuid>?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","meaning":"new independent action creation requires zero; old program risk-governance ledger revision removed"}`.
- **Native facts:** `RiskTreatmentActionAdded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"RiskTreatmentActionRegistration","firstHttpStatus":200,"terminalResultType":"RiskTreatmentActionRegistration"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/AddRiskTreatmentAction.cs](../../../../src/Compliance.Common/Features/Risks/Commands/AddRiskTreatmentAction.cs).
- **Selected changes:** Add required ActionId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## AmendAuditPackage

- **Handler → first owner:** `AmendAuditPackageHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/amendments/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `ExpectedRevision: long`, `AmendmentReason: string`, `IncludedRecordIds: IReadOnlyList<Uuid>?`, `Content: AuditPackageContent?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditPackageAmended`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/AmendAuditPackage.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/AmendAuditPackage.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/AmendAuditPackageHandlerTests.cs`.

## AmendSampleSelection

- **Handler → first owner:** `AmendSampleSelectionHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"SelectionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/amend-sample-selection/{selection_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `ExaminationId: Uuid`, `AuditorRequestId: Uuid`, `ExpectedRequestRevision: long`, `SelectionId: Uuid`, `SupersedesSelectionId: Uuid`, `PopulationId: Uuid`, `PopulationVersion: int`, `PopulationRowIds: IReadOnlyList<Uuid>`, `SelectionMethod: string`, `Source: string`, `ExternalReference: string`, `AmendmentReason: string`.
- **Revision scope:** `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"open request admission revision, not new selection revision zero"}`.
- **Native facts:** `AuditorSampleSelectionAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/AmendSampleSelection.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/AmendSampleSelection.cs).
- **Selected changes:** Admit exact immutable selection on the open request before materializing child. Amendment keeps prior population identity and strictly later population version. Remove the accidental comparison of request revision to child zero.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestTests.cs`, `test/Compliance.Tests/Features/AuditorRequests/SampleSelectionCommandHandlerTests.cs`.

## AmendServiceEngagement

- **Handler → first owner:** `AmendServiceEngagementHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/amend-service-engagement/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `ExpectedRevision: long`, `Content: ServiceEngagementDraftContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementMutationRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/AmendServiceEngagement.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/AmendServiceEngagement.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## AnnotateReadinessGap

- **Handler → first owner:** `AnnotateReadinessGapHandler` → `ReadinessGapPlan`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `AssessmentId: Uuid`, `GapId: Uuid`, `ExpectedRevision: long`, `Body: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ReadinessGapPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ReadinessGapAnnotated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ReadinessAnnotationView","firstHttpStatus":200,"terminalResultType":"ReadinessAnnotationView"}`.
- **Owner protocols:** Latest readiness and gap-plan decision seal.
- **Preserved source validation:** [src/Compliance.Common/Features/Readiness/AnnotateReadinessGap.cs](../../../../src/Compliance.Common/Features/Readiness/Commands/AnnotateReadinessGap.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Readiness/PersonalReadinessAnnotationTransportTests.cs`, `test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs`.

## ApplyApplicationImportEffect

- **Handler → first owner:** `ApplyApplicationImportEffectHandler` → `DeclaredApplication`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"UUIDv5(source eventId, length-prefixed effectKind + canonical target stream + source lifecycle episode); external process callback source is the immutable accepted business source event plus exact effect key, never attemptId."}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `Source: SourceFactProof<ApplicationImportExecutionAuthorized>`, `Target: EventStreamAddress`, `OperationId: Uuid`, `Generation: long`, `ExpectedRevision: long?`, `Intent: ImmutableContentRef`, `BatchId: Uuid`, `ApplicationId: Uuid`, `RowGroup: ImmutableContentRef`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":"Explicit null requires exact source authorization/generation and physical append OCC; never substitute current logical revision."}`.
- **Native facts:** `ApplicationImportEffectPending`.
- **Resolved intent:** `ImportTargetGroupIntent`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/ApplyApplicationImportEffect.cs](../../../../src/Compliance.Common/Features/Applications/Commands/ApplyApplicationImportEffect.cs).
- **Selected changes:** Add required CommandId. Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportPendingEffectTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportEffectVerificationTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs`.

## ApproveAccessExpectation

- **Handler → first owner:** `ApproveAccessExpectationHandler` → `AccessExpectation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `SystemInstanceId: Uuid`, `ExpectationId: Uuid`, `ExpectedRevision: long`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessExpectationApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessExpectationView","firstHttpStatus":200,"terminalResultType":"AccessExpectationView"}`.
- **Owner protocols:** Expectation succession and population exceptions.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ApproveAccessExpectation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ApproveAccessExpectation.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs`.

## ApproveApplicationSuccessor

- **Handler → first owner:** `ApproveApplicationSuccessorHandler` → `ApplicationRelationship`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/applications/{predecessor_application_id}/successors/{successor_application_id}/approval/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `PredecessorApplicationId: Uuid`, `SuccessorApplicationId: Uuid`, `ExpectedSourceRevision: long`, `ExpectedTargetRevision: long`, `ExpectedRevision: long`, `ImpactDigest: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationRelationshipApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/Commands/ApproveApplicationSuccessor.cs](../../../../src/Compliance.Common/Features/Applications/Commands/ApproveApplicationSuccessor.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationSuccessorApprovalTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs`.

## ApproveAuditPackageDisclosure

- **Handler → first owner:** `ApproveAuditPackageDisclosureHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-disclosure/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `PackageVersion: long`, `Recipient: string`, `Purpose: string`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditPackageDisclosureApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageDisclosure.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/ApproveAuditPackageDisclosure.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs`.

## ApproveAuditPackageValidationOverride

- **Handler → first owner:** `ApproveAuditPackageValidationOverrideHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/approve-audit-package-validation-override/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `PackageVersion: long`, `ValidationIssue: string`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditPackageValidationOverrideApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageValidationOverride.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/ApproveAuditPackageValidationOverride.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs`.

## ApproveCampaignWaiver

- **Handler → first owner:** `ApproveCampaignWaiverHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `CampaignId: Uuid`, `PersonId: Uuid`, `Reason: string`, `ExpiresOn: DateOnly`, `ExpectedCampaignRevision: long`.
- **Revision scope:** `{"field":"ExpectedCampaignRevision","owner":"PolicyDistributionCampaign","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `PolicyParticipantResultAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignWaiverView"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/ApproveCampaignWaiver.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/ApproveCampaignWaiver.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedCampaignRevision; business Version remains an exact content version, not owner OCC. Admit complete participant result on the open campaign generation first; apply to participant through committed admission fact. Personal source actor is retained.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs`, `test/Compliance.Tests/Features/PolicyDistribution/PersonalCampaignWaiverTransportTests.cs`.

## ApproveControlOperatingPlan

- **Handler → first owner:** `ApproveControlOperatingPlanHandler` → `ControlOperatingPlan`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `ExpectedRevision: long`, `PlanVersionId: Uuid`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOperatingPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOperatingPlanApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOperatingPlanView","firstHttpStatus":200,"terminalResultType":"ControlOperatingPlanView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/ApproveControlOperatingPlan.cs](../../../../src/Compliance.Common/Features/Operations/Commands/ApproveControlOperatingPlan.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs`, `test/Compliance.Tests/Features/Operations/PersonalOperatingPlanApprovalTransportTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`, `test/Compliance.Tests/Features/Work/WorkAssignmentTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/ControlOperatingPlanWorkTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## ApproveSystemDescription

- **Handler → first owner:** `ApproveSystemDescriptionHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/approve-system-description/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `Version: long`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionApproved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/ApproveSystemDescription.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/ApproveSystemDescription.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## AssignAuditorRequest

- **Handler → first owner:** `AssignAuditorRequestHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/assign-auditor-request/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`, `AssignmentReason: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorRequestAssigned`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/AssignAuditorRequest.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/AssignAuditorRequest.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## AssignRiskOwner

- **Handler → first owner:** `AssignRiskOwnerHandler` → `RiskGovernance`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assign-risk-owner/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ExpectedRevision: long`, `PersonId: Uuid`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskGovernance","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskOwnerAssigned`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/AssignRiskOwner.cs](../../../../src/Compliance.Common/Features/Risks/Commands/AssignRiskOwner.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## AssignServiceEngagementStaff

- **Handler → first owner:** `AssignServiceEngagementStaffHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `StaffMemberId: Uuid`, `ExpectedRevision: long`, `EffectiveFrom: DateOnly`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementStaffAssignmentRequested`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ServiceEngagementAcceptanceView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/AssignServiceEngagementStaff.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/AssignServiceEngagementStaff.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. Positive actual-staff assignment acquires the same exact current service-epoch, partner authority and permanent staff/user practice commitments as acceptance before emitting ServiceEngagementStaffAssigned.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs`.

## AttestControlOccurrence

- **Handler → first owner:** `AttestControlOccurrenceHandler` → `ControlOccurrence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `OccurrenceId: Uuid`, `ExpectedRevision: long`, `Result: string`, `PerformedAt: DateTimeOffset`, `CoveredFrom: DateOnly?`, `CoveredUntil: DateOnly?`, `Notes: string?`, `Rationale: string?`, `Evidence: IReadOnlyList<EvidenceReference>`, `PerformedByPersonId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOccurrenceAttested`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/AttestControlOccurrence.cs](../../../../src/Compliance.Common/Features/Operations/Commands/AttestControlOccurrence.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`, `test/Compliance.Tests/Features/Work/ConcurrentOccurrenceSubmissionTests.cs`.

## BindSystemDescriptionSources

- **Handler → first owner:** `BindSystemDescriptionSourcesHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/bind-system-description-sources/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `ExpectedRevision: long`, `SourceVersionIds: IReadOnlyList<Uuid>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionSourcesBound`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/BindSystemDescriptionSources.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/BindSystemDescriptionSources.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionDraftCommandHandlerTests.cs`.

## BindSystemDescriptionToExamination

- **Handler → first owner:** `BindSystemDescriptionToExaminationHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/bind-system-description-to-examination/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `Version: long`, `ExaminationId: Uuid`, `BindingPurpose: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionBound`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/BindSystemDescriptionToExamination.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/BindSystemDescriptionToExamination.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## CancelApplicationImport

- **Handler → first owner:** `CancelApplicationImportHandler` → `ApplicationImportBatch`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `BatchId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationImportCancellationAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Complete batch application and publication.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/CancelApplicationImport.cs](../../../../src/Compliance.Common/Features/Applications/Commands/CancelApplicationImport.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportLifecycleHandlerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs`.

## CancelEvidenceRequest

- **Handler → first owner:** `CancelEvidenceRequestHandler` → `EvidenceRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `EvidenceRequestId: Uuid`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"EvidenceRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `EvidenceRequestCancelled`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}`.
- **Owner protocols:** Independent evidence fulfilment.
- **Preserved source validation:** [src/Compliance.Common/Features/Evidence/CancelEvidenceRequest.cs](../../../../src/Compliance.Common/Features/Evidence/Commands/CancelEvidenceRequest.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`.

## CancelRiskTreatmentAction

- **Handler → first owner:** `CancelRiskTreatmentActionHandler` → `RiskTreatmentAction`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ActionId: Uuid`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskTreatmentActionCancelled`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/CancelRiskTreatmentAction.cs](../../../../src/Compliance.Common/Features/Risks/Commands/CancelRiskTreatmentAction.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## ClassifyAccessPrincipal

- **Handler → first owner:** `ClassifyAccessPrincipalHandler` → `AccessPrincipalClassification`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `PopulationId: Uuid`, `ProviderSubjectId: string`, `ExpectedRevision: long`, `Classification: string`, `Rationale: string`, `PersonId: Uuid?`, `ServiceIdentityId: Uuid?`, `AccountableOwnerPersonId: Uuid?`, `SharedJustification: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessPrincipalClassification","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessPrincipalClassified`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessPrincipalClassificationView","firstHttpStatus":200,"terminalResultType":"AccessPrincipalClassificationView"}`.
- **Owner protocols:** Population acceptance and principal classification.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ClassifyAccessPrincipal.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ClassifyAccessPrincipal.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedClassificationCount to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs`.

## CloseAdvisorHandoff

- **Handler → first owner:** `CloseAdvisorHandoffHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/closures/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorHandoff","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorHandoffClosed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/CloseAdvisorHandoff.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CloseAdvisorHandoff.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## CloseCampaign

- **Handler → first owner:** `CloseCampaignHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `CampaignId: Uuid`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"PolicyDistributionCampaign","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `PolicyCampaignClosureAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignView"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/CloseCampaign.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/CloseCampaign.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## CloseFinding

- **Handler → first owner:** `CloseFindingHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedRevision: long`, `VerificationRationale: string`, `ResolutionEvidence: IReadOnlyList<EvidenceReference>`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `FindingClosureAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/CloseFinding.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/CloseFinding.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`, `test/Compliance.Tests/Features/Readiness/PersonalReadinessClosureTransportTests.cs`, `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## CloseProviderCoverageGap

- **Handler → first owner:** `CloseProviderCoverageGapHandler` → `ProviderAssuranceGap`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/close-provider-coverage-gap/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `GapId: Uuid`, `ExpectedRevision: long`, `Content: ProviderCoverageGapClosureContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ProviderAssuranceGap","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ProviderCoverageGapClosed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ProviderCoverageGapRegistration","firstHttpStatus":200,"terminalResultType":"ProviderCoverageGapRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/CloseProviderCoverageGap.cs](../../../../src/Compliance.Common/Features/Providers/Commands/CloseProviderCoverageGap.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## CloseServiceEngagement

- **Handler → first owner:** `CloseServiceEngagementHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementMutationRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/CloseServiceEngagement.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/CloseServiceEngagement.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## CompleteAccessReviewCampaign

- **Handler → first owner:** `CompleteAccessReviewCampaignHandler` → `AccessReviewCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ExpectedRevision: long`, `Attestation: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewCampaign","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessReviewCampaignCompletionAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"AccessReviewCampaignCompletionView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/CompleteAccessReviewCampaign.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/CompleteAccessReviewCampaign.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs`, `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/RestrictedAccessReviewCampaignReadTests.cs`.

## CompleteCorrectiveAction

- **Handler → first owner:** `CompleteCorrectiveActionHandler` → `CorrectiveAction`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedRevision: long`, `ActionId: Uuid`, `ResolutionNotes: string`, `Evidence: IReadOnlyList<EvidenceReference>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"CorrectiveAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `CorrectiveActionCompletedV2`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/CompleteCorrectiveAction.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/CompleteCorrectiveAction.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`, `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## CorrectAdvisorFeedback

- **Handler → first owner:** `CorrectAdvisorFeedbackHandler` → `AdvisorFeedbackThread`.
- **Logical identity:** `{"field":"CorrectionId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections/{correction_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `FeedbackId: Uuid`, `CorrectionId: Uuid`, `CorrectedBody: string`, `CorrectionReason: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorFeedbackCorrected`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/CorrectAdvisorFeedback.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CorrectAdvisorFeedback.cs).
- **Selected changes:** Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Readiness/AdvisoryReadinessNoteIndependenceTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/CorrectAdvisorFeedbackHandlerTests.cs`.

## CorrectControlAttestation

- **Handler → first owner:** `CorrectControlAttestationHandler` → `ControlOccurrence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `OccurrenceId: Uuid`, `ExpectedRevision: long`, `Result: string`, `PerformedAt: DateTimeOffset`, `CoveredFrom: DateOnly?`, `CoveredUntil: DateOnly?`, `Notes: string?`, `Rationale: string?`, `Evidence: IReadOnlyList<EvidenceReference>`, `CorrectionReason: string`, `PerformedByPersonId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOccurrenceAttested`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/CorrectControlAttestation.cs](../../../../src/Compliance.Common/Features/Operations/Commands/CorrectControlAttestation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Operations/PersonalOccurrenceProofTransportTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`, `test/Compliance.Tests/Features/Operations/ControlOccurrenceTests.cs`, `test/Compliance.Tests/Features/Work/FitzControlOccurrenceWorkItemDirectoryTests.cs`, `test/Compliance.Tests/Features/Work/NotApplicableOccurrenceReviewTests.cs`.

## CorrelateApplicationImportRow

- **Handler → first owner:** `CorrelateApplicationImportRowHandler` → `ApplicationImportBatch`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `BatchId: Uuid`, `RowId: Uuid`, `ExpectedRevision: long`, `Decision: string`, `ApplicationId: Uuid?`, `ExpectedApplicationRevision: long?`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationImportRowCorrelated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Complete batch application and publication.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/CorrelateApplicationImportRow.cs](../../../../src/Compliance.Common/Features/Applications/Commands/CorrelateApplicationImportRow.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportCommitTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportMissingPreviewTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportFrozenPlanTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportCorrelationTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportRetirementPlanTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportAcceptancePlanTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportSourceClaimTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportReconciliationPreviewTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportPreviewCorrelationTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportReadTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportPendingEffectTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportRetirementEffectTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportLifecycleHandlerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportEffectVerificationTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs`.

## CreateAdvisorHandoff

- **Handler → first owner:** `CreateAdvisorHandoffHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"HandoffId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `RecordVersions: IReadOnlyList<AdvisorRecordVersion>`, `Purpose: string`, `ExpiresOn: DateOnly`, `ComparedToHandoffId: Uuid?`.
- **Revision scope:** `{"field":null,"owner":"AdvisorHandoff","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AdvisorHandoffCreated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/CreateAdvisorHandoff.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/CreateAdvisorHandoff.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Controls/ControlDraftAuthorizationTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## CreateAuditPackage

- **Handler → first owner:** `CreateAuditPackageHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"PackageId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `Content: AuditPackageContent`.
- **Revision scope:** `{"field":null,"owner":"AuditPackageManifest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AuditPackageCreated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/CreateAuditPackage.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/CreateAuditPackage.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/CreateAuditPackageHandlerTests.cs`, `test/Compliance.Tests/Features/AuditPackages/CreateAuditPackageContractTests.cs`.

## CreateAuditResponseDraft

- **Handler → first owner:** `CreateAuditResponseDraftHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"ResponseId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/create-audit-response-draft/{response_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `Summary: string`, `EvidenceArtifactIds: IReadOnlyList<Uuid>`, `ExpectedRequestRevision: long`.
- **Revision scope:** `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `AuditorResponseAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/CreateAuditResponseDraft.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/CreateAuditResponseDraft.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## CreateServiceEngagement

- **Handler → first owner:** `CreateServiceEngagementHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"EngagementId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/create-service-engagement`; MCP enabled.
- **Exact fields:** `TenantId: Uuid`, `EngagementId: Uuid`, `ExpectedRevision: long`, `Content: ServiceEngagementDraftContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","meaning":"new independent owner creation requires zero; former directory/tenant/system ledger revision is not reused"}`.
- **Native facts:** `ServiceEngagementMutationRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/CreateServiceEngagement.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/CreateServiceEngagement.cs).
- **Selected changes:** Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/IndependenceAssignmentBoundaryTests.cs`, `test/Compliance.Tests/Features/AccessControl/ServiceEngagementHandlerTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## CreateSystemDescriptionDraft

- **Handler → first owner:** `CreateSystemDescriptionDraftHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"DescriptionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/create-system-description-draft`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `ExaminationId: Uuid`, `Title: string`, `Sections: IReadOnlyList<SystemDescriptionSection>`.
- **Revision scope:** `{"field":null,"owner":"SystemDescription","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `SystemDescriptionDraftCreated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/CreateSystemDescriptionDraft.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/CreateSystemDescriptionDraft.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionDraftCommandHandlerTests.cs`.

## DecideReadiness

- **Handler → first owner:** `DecideReadinessHandler` → `ProgramReadiness`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `AssessmentId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ProgramReadiness","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ReadinessDecisionAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ReadinessDecisionView"}`.
- **Owner protocols:** Latest readiness and gap-plan decision seal.
- **Preserved source validation:** [src/Compliance.Common/Features/Readiness/DecideReadiness.cs](../../../../src/Compliance.Common/Features/Readiness/Commands/DecideReadiness.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Readiness/PersonalReadinessClosureTransportTests.cs`, `test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs`.

## DecideTypeIEntry

- **Handler → first owner:** `DecideTypeIEntryHandler` → `ProgramReadiness`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `AssessmentId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `AcknowledgedGapIds: IReadOnlyList<Uuid>?`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ProgramReadiness","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `TypeIEntryDecisionAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"TypeIEntryDecisionView"}`.
- **Owner protocols:** Latest readiness and gap-plan decision seal.
- **Preserved source validation:** [src/Compliance.Common/Features/Readiness/DecideTypeIEntry.cs](../../../../src/Compliance.Common/Features/Readiness/Commands/DecideTypeIEntry.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Programs/ProgramStageProjectionTests.cs`, `test/Compliance.Tests/Features/Readiness/PersonalTypeIEntryTransportTests.cs`, `test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs`, `test/Compliance.Tests/Features/Examinations/ExaminationEngagementCommandHandlerTests.cs`.

## DeclareApplication

- **Handler → first owner:** `DeclareApplicationHandler` → `DeclaredApplication`.
- **Logical identity:** `{"field":"ApplicationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}`; MCP enabled.
- **Exact fields:** `ApplicationId: Uuid`, `TenantId: Uuid`, `Name: string`, `Purpose: string`, `OwnerReference: string?`, `Classification: string?`, `SystemOwnerPersonId: Uuid?`, `AccessOwnerPersonId: Uuid?`, `IsRestricted: bool`, `ApplicationKind: string?`, `Criticality: string?`.
- **Revision scope:** `{"field":null,"owner":"DeclaredApplication","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ApplicationDeclared`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ApplicationRegistration","firstHttpStatus":200,"terminalResultType":"ApplicationRegistration"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/DeclareApplication.cs](../../../../src/Compliance.Common/Features/Applications/Commands/DeclareApplication.cs).
- **Selected changes:** Add required ApplicationId. Remove optional DeclarationKey; ApplicationId is the required creation key.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs`, `test/Compliance.Tests/Features/Applications/PersonalAccessReviewScopeTransportTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationOwnerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationInventoryAuthorizerTests.cs`, `test/Compliance.Tests/Features/Examinations/ReviewedSystemTypeIBaselineSourceContributorTests.cs`.

## DefineTrainingRequirement

- **Handler → first owner:** `DefineTrainingRequirementHandler` → `TrainingRequirement`.
- **Logical identity:** `{"field":"RequirementId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}`; MCP enabled.
- **Exact fields:** `RequirementId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `Identifier: string`, `Content: TrainingRequirementContent`.
- **Revision scope:** `{"field":null,"owner":"TrainingRequirement","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `TrainingRequirementDefined`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"TrainingRequirementRegistration","firstHttpStatus":200,"terminalResultType":"TrainingRequirementRegistration"}`.
- **Owner protocols:** Independent training requirement.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/DefineTrainingRequirement.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/DefineTrainingRequirement.cs).
- **Selected changes:** Add required RequirementId. Supplied RequirementId must equal existing TrainingCatalog.IdFor(TenantId, ProgramId, Identifier); keep NormalizeIdentifier and its exact UUIDv5 algorithm unchanged.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`.

## DeliverAuditResponse

- **Handler → first owner:** `DeliverAuditResponseHandler` → `AuditorResponse`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/deliver-audit-response/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `Version: long`, `Recipient: string`, `DeliveryReference: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorResponseDelivered`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/DeliverAuditResponse.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/DeliverAuditResponse.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## DisposeControlEvaluationDeviation

- **Handler → first owner:** `DisposeControlEvaluationDeviationHandler` → `ControlEvaluation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `EvaluationId: Uuid`, `DeviationId: Uuid`, `ExpectedRevision: long`, `Disposition: string`, `Rationale: string`, `WaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlEvaluationDeviationDisposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}`.
- **Owner protocols:** Independent evaluation and retest.
- **Preserved source validation:** [src/Compliance.Common/Features/Evaluations/DisposeControlEvaluationDeviation.cs](../../../../src/Compliance.Common/Features/Evaluations/Commands/DisposeControlEvaluationDeviation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs`.

## EvaluateClientIndependence

- **Handler → first owner:** `EvaluateClientIndependenceHandler` → `ClientServiceIndependence`.
- **Logical identity:** `{"field":"EvaluationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id}`; MCP enabled.
- **Exact fields:** `TenantId: Uuid`, `EvaluationId: Uuid`, `RuleSetVersion: long`, `ExaminationPeriodStart: DateOnly`, `ExpectedServiceEpoch: long`.
- **Revision scope:** `{"field":"ExpectedServiceEpoch","owner":"ClientServiceIndependence","meaning":"current complete service history epoch, never new preview child revision"}`.
- **Native facts:** `ClientIndependenceEvaluationAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"IndependenceEvaluationView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/EvaluateClientIndependence.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/EvaluateClientIndependence.cs).
- **Selected changes:** Freeze complete as-of service/practice/rule input snapshot on source admission. Child computes pure historical preview and always keeps ProductionAcceptanceBlocked=true.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/IndependenceOperationTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## ExecuteApplicationImport

- **Handler → first owner:** `ExecuteApplicationImportHandler` → `ApplicationImportBatch`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `BatchId: Uuid`.
- **Revision scope:** `{"field":null,"owner":"ApplicationImportBatch","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ApplicationImportExecutionAuthorized`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Complete batch application and publication.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/ExecuteApplicationImport.cs](../../../../src/Compliance.Common/Features/Applications/Commands/ExecuteApplicationImport.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportExecutionTests.cs`.

## ExemptAccessExpectation

- **Handler → first owner:** `ExemptAccessExpectationHandler` → `AccessExpectation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `SystemInstanceId: Uuid`, `ExpectationId: Uuid`, `ExpectedRevision: long`, `ProviderSubjectId: string`, `Rationale: string`, `ExpiresAt: DateTimeOffset`, `ProviderEntitlementId: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessExpectationExceptionRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessExpectationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessExpectationExceptionView"}`.
- **Owner protocols:** Expectation succession and population exceptions.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ExemptAccessExpectation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptAccessExpectation.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs`.

## ExemptAccessRemediation

- **Handler → first owner:** `ExemptAccessRemediationHandler` → `AccessReviewItem`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ItemId: Uuid`, `ExpectedRevision: long`, `Rationale: string`, `ExpiresAt: DateTimeOffset?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessRemediationExceptionRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessRemediationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessRemediationExceptionView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ExemptAccessRemediation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptAccessRemediation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs`, `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs`.

## ExemptMissingAccessPopulation

- **Handler → first owner:** `ExemptMissingAccessPopulationHandler` → `AccessPopulationException`.
- **Logical identity:** `{"field":"ExceptionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id}`; MCP disabled.
- **Exact fields:** `ExceptionId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `SystemInstanceId: Uuid`, `ExpectedRevision: long`, `Reason: string`, `ExpiresAt: DateTimeOffset`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessPopulationException","meaning":"new independent owner creation requires zero; former directory/tenant/system ledger revision is not reused"}`.
- **Native facts:** `AccessPopulationExceptionRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessPopulationExceptionView","firstHttpStatus":200,"terminalResultType":"AccessPopulationExceptionView"}`.
- **Owner protocols:** Expectation succession and population exceptions.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ExemptMissingAccessPopulation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ExemptMissingAccessPopulation.cs).
- **Selected changes:** Add required ExceptionId. Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`.

## FreezeApplicationImportOmissionProposal

- **Handler → first owner:** `FreezeApplicationImportOmissionProposalHandler` → `ApplicationImportBatch`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `BatchId: Uuid`, `ExpectedRevision: long`, `ExpectedSourcePosition: ulong`, `ExpectedContentSha256: string`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationImportBatch","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationImportRetirementProposalSealed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Complete batch application and publication.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/FreezeApplicationImportOmissionProposal.cs](../../../../src/Compliance.Common/Features/Applications/Commands/FreezeApplicationImportOmissionProposal.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedBatchRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs`.

## FulfilEvidenceRequest

- **Handler → first owner:** `FulfilEvidenceRequestHandler` → `EvidenceRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `EvidenceRequestId: Uuid`, `ExpectedRevision: long`, `ArtifactId: Uuid`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"EvidenceRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `EvidenceRequestFulfilled`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}`.
- **Owner protocols:** Independent evidence fulfilment.
- **Preserved source validation:** [src/Compliance.Common/Features/Evidence/FulfilEvidenceRequest.cs](../../../../src/Compliance.Common/Features/Evidence/Commands/FulfilEvidenceRequest.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs`.

## GenerateAuditPopulationExport

- **Handler → first owner:** `GenerateAuditPopulationExportHandler` → `AuditPopulationExportProcess`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** technical_process_request; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PopulationId: Uuid`, `Version: long`, `Format: string`, `ExportKey: string`.
- **Revision scope:** `{"field":null,"owner":"AuditPopulationExportProcess","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** Process CAS only..
- **Original response:** `{"mode":"internal","responseType":"AuditPopulationExportReceipt","firstHttpStatus":null,"terminalResultType":"AuditPopulationExportReceipt"}`.
- **Owner protocols:** Retention basis and authoritative legal-hold barrier.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPopulations/GenerateAuditPopulationExport.cs](../../../../src/Compliance.Common/Features/AuditPopulations/Commands/GenerateAuditPopulationExport.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPopulations/GenerateAuditPopulationExportHandlerTests.cs`.

## GenerateSystemDescriptionExport

- **Handler → first owner:** `GenerateSystemDescriptionExportHandler` → `SystemDescriptionExportProcess`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** technical_process_request; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `Version: long`, `Format: string`, `ExportKey: string`.
- **Revision scope:** `{"field":null,"owner":"SystemDescriptionExportProcess","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** Process CAS only..
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/GenerateSystemDescriptionExport.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/GenerateSystemDescriptionExport.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## InviteGuestConsultant

- **Handler → first owner:** `InviteGuestConsultantHandler` → `TenantInvitation`.
- **Logical identity:** `{"field":"InvitationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/guest-invitations/{invitation_id}`; MCP enabled.
- **Exact fields:** `InvitationId: Uuid`, `TenantId: Uuid`, `EmailAddress: string`, `ProgramIds: IReadOnlyList<Uuid>`, `ExpiresOn: DateOnly`.
- **Revision scope:** `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `TenantMemberInvited`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Mailbox generation, invitation and delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/Tenants/InviteGuestConsultant.cs](../../../../src/Compliance.Common/Features/Tenants/Commands/InviteGuestConsultant.cs).
- **Selected changes:** Add required InvitationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/RbacManagementAuthorizerTests.cs`, `test/Compliance.Tests/Features/Tenants/InviteGuestConsultantHandlerTests.cs`.

## InviteOrganizationMember

- **Handler → first owner:** `InviteOrganizationMemberHandler` → `TenantInvitation`.
- **Logical identity:** `{"field":"InvitationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/member-invitations/{invitation_id}`; MCP enabled.
- **Exact fields:** `InvitationId: Uuid`, `TenantId: Uuid`, `EmailAddress: string`, `BuiltInRole: string`.
- **Revision scope:** `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `TenantMemberInvited`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Mailbox generation, invitation and delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/Tenants/InviteOrganizationMember.cs](../../../../src/Compliance.Common/Features/Tenants/Commands/InviteOrganizationMember.cs).
- **Selected changes:** Add required InvitationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs`.

## InviteTenantMember

- **Handler → first owner:** `InviteTenantMemberHandler` → `TenantInvitation`.
- **Logical identity:** `{"field":"InvitationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/invitations/{invitation_id}`; MCP enabled.
- **Exact fields:** `InvitationId: Uuid`, `TenantId: Uuid`, `EmailAddress: string`, `Affiliation: string`, `Administrator: bool`.
- **Revision scope:** `{"field":null,"owner":"TenantInvitation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `TenantMemberInvited`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Mailbox generation, invitation and delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/Tenants/InviteTenantMember.cs](../../../../src/Compliance.Common/Features/Tenants/Commands/InviteTenantMember.cs).
- **Selected changes:** Add required InvitationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## LaunchAccessReviewCampaign

- **Handler → first owner:** `LaunchAccessReviewCampaignHandler` → `AccessReviewCampaign`.
- **Logical identity:** `{"field":"CampaignId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}`; MCP enabled.
- **Exact fields:** `CampaignId: Uuid`, `TenantId: Uuid`, `Name: string`, `Instructions: string`, `Deadline: DateTimeOffset`, `Assignments: IReadOnlyList<AccessReviewAssignment>`, `ProgramId: Uuid?`, `RemediationOwnerMemberId: Uuid?`.
- **Revision scope:** `{"field":null,"owner":"AccessReviewCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AccessReviewCampaignLaunched`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessReviewCampaignRegistration","firstHttpStatus":200,"terminalResultType":"AccessReviewCampaignRegistration"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/LaunchAccessReviewCampaign.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/LaunchAccessReviewCampaign.cs).
- **Selected changes:** Add required CampaignId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewInfrastructureTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs`.

## LaunchPolicyCampaign

- **Handler → first owner:** `LaunchPolicyCampaignHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CampaignId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns/{campaign_id}`; MCP enabled.
- **Exact fields:** `CampaignId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PolicyId: Uuid`, `PolicyVersion: long`, `RosterSnapshotId: Uuid`, `DueOn: DateOnly`, `Instructions: string?`.
- **Revision scope:** `{"field":null,"owner":"PolicyDistributionCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `PolicyCampaignLaunched`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"CampaignRegistration","firstHttpStatus":200,"terminalResultType":"CampaignRegistration"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/LaunchPolicyCampaign.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/LaunchPolicyCampaign.cs).
- **Selected changes:** Add required CampaignId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs`, `test/Compliance.Tests/Features/PolicyDistribution/PersonalCampaignWaiverTransportTests.cs`.

## LaunchTrainingCampaign

- **Handler → first owner:** `LaunchTrainingCampaignHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CampaignId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns/{campaign_id}`; MCP enabled.
- **Exact fields:** `CampaignId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequirementId: Uuid`, `RequirementVersion: long`, `RosterSnapshotId: Uuid`, `DueOn: DateOnly`, `Instructions: string?`.
- **Revision scope:** `{"field":null,"owner":"PolicyDistributionCampaign","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `PolicyCampaignLaunched`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"CampaignRegistration","firstHttpStatus":200,"terminalResultType":"CampaignRegistration"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/LaunchTrainingCampaign.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/LaunchTrainingCampaign.cs).
- **Selected changes:** Add required CampaignId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## LinkFindingAcceptance

- **Handler → first owner:** `LinkFindingAcceptanceHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedRevision: long`, `Kind: string`, `RecordId: Uuid`, `DecisionId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `FindingAcceptanceLinkedV2`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/LinkFindingAcceptance.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/LinkFindingAcceptance.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## LinkProviderCoverageGapRiskAcceptance

- **Handler → first owner:** `LinkProviderCoverageGapRiskAcceptanceHandler` → `ProviderAssuranceGap`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ProviderId: Uuid`, `GapId: Uuid`, `ExpectedGapRevision: long`, `RiskId: Uuid`, `AcceptanceId: Uuid`.
- **Revision scope:** `{"field":"ExpectedGapRevision","owner":"ProviderAssuranceGap","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `ProviderCoverageGapRiskAcceptanceAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderCoverageGapRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/LinkProviderCoverageGapRiskAcceptance.cs](../../../../src/Compliance.Common/Features/Providers/Commands/LinkProviderCoverageGapRiskAcceptance.cs).
- **Selected changes:** Add required CommandId. Open-gap and duplicate (RiskId, AcceptanceId) checks linearize on gap admission. Child LinkId is this CommandId; materialization preserves admission time even if gap subsequently closes or acceptance later expires.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## MarkAdvisorHandoffReadyForReview

- **Handler → first owner:** `MarkAdvisorHandoffReadyForReviewHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/ready-for-review/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorHandoff","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorHandoffReadiedForReview`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/MarkAdvisorHandoffReadyForReview.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/MarkAdvisorHandoffReadyForReview.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Controls/ControlDraftAuthorizationTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## MigrateFindingHistory

Disposable pre-release history replacement; delete this migration command, producer, registration and readers after direct cutover.

## OpenAccessPopulation

- **Handler → first owner:** `OpenAccessPopulationHandler` → `AccessPopulation`.
- **Logical identity:** `{"field":"PopulationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-populations/{population_id}`; MCP enabled.
- **Exact fields:** `PopulationId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `SystemInstanceId: Uuid`, `ExpectedSystemInstanceRevision: long`, `ObservedAt: DateTimeOffset`, `Source: string`.
- **Revision scope:** `{"field":null,"owner":"AccessPopulation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AccessPopulationOpened`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessPopulationRegistration","firstHttpStatus":200,"terminalResultType":"AccessPopulationRegistration"}`.
- **Owner protocols:** Population acceptance and principal classification.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/OpenAccessPopulation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/OpenAccessPopulation.cs).
- **Selected changes:** Add required PopulationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`, `test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs`.

## OpenControlOccurrence

- **Handler → first owner:** `OpenControlOccurrenceHandler` → `ControlOccurrence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{command_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `Trigger: string`, `OccurredOn: DateOnly`, `ExpectedRevision: long`, `CommandId: Uuid`, `BusinessWindow: ControlBusinessWindow`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOccurrenceOpened`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/OpenControlOccurrence.cs](../../../../src/Compliance.Common/Features/Operations/Commands/OpenControlOccurrence.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. Resolve OccurrenceId deterministically from tenant/program/control/BusinessWindow across all generators. CommandId distinguishes authored opening attempts; trigger and date cannot create a second occurrence for the same window.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs`, `test/Compliance.Tests/Features/Operations/ControlOccurrenceTests.cs`, `test/Compliance.Tests/Features/Work/FitzControlOccurrenceWorkItemDirectoryTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/NotApplicableOccurrenceReviewTests.cs`.

## OpenEvidenceRequest

- **Handler → first owner:** `OpenEvidenceRequestHandler` → `EvidenceRequest`.
- **Logical identity:** `{"field":"EvidenceRequestId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}`; MCP enabled.
- **Exact fields:** `EvidenceRequestId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `Title: string`, `Instructions: string`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`, `ControlId: Uuid?`.
- **Revision scope:** `{"field":null,"owner":"EvidenceRequest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `EvidenceRequestOpened`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"EvidenceRequestView","firstHttpStatus":200,"terminalResultType":"EvidenceRequestView"}`.
- **Owner protocols:** Independent evidence fulfilment.
- **Preserved source validation:** [src/Compliance.Common/Features/Evidence/OpenEvidenceRequest.cs](../../../../src/Compliance.Common/Features/Evidence/Commands/OpenEvidenceRequest.cs).
- **Selected changes:** Add required EvidenceRequestId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueSearchTests.cs`, `test/Compliance.Tests/Features/Work/WorkQueueTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`.

## PlaceArtifactLegalHold

- **Handler → first owner:** `PlaceArtifactLegalHoldHandler` → `ArtifactLegalHold`.
- **Logical identity:** `{"field":"HoldId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `SourceKind: string`, `SourceId: Uuid`, `ExpectedContentSha256: string`, `HoldId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ArtifactLegalHold","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ArtifactLegalHoldPlaced`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Retention basis and authoritative legal-hold barrier.
- **Preserved source validation:** [src/Compliance.Common/Features/Retention/PlaceArtifactLegalHold.cs](../../../../src/Compliance.Common/Features/Retention/Commands/PlaceArtifactLegalHold.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs`, `test/Compliance.Tests/Features/Retention/AttestRetentionManagementWriteWallTests.cs`.

## PlanReadinessGap

- **Handler → first owner:** `PlanReadinessGapHandler` → `ReadinessGapPlan`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan-readiness-gap/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `GapId: Uuid`, `ExpectedRevision: long`, `OwnerMemberId: Uuid`, `TargetDate: DateOnly`, `Action: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ReadinessGapPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ReadinessGapPlanned`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ReadinessGapPlanView","firstHttpStatus":200,"terminalResultType":"ReadinessGapPlanView"}`.
- **Owner protocols:** Latest readiness and gap-plan decision seal.
- **Preserved source validation:** [src/Compliance.Common/Features/Readiness/PlanReadinessGap.cs](../../../../src/Compliance.Common/Features/Readiness/Commands/PlanReadinessGap.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs`.

## ProposeAccessExpectation

- **Handler → first owner:** `ProposeAccessExpectationHandler` → `AccessExpectation`.
- **Logical identity:** `{"field":"ExpectationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}`; MCP enabled.
- **Exact fields:** `ExpectationId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `SystemInstanceId: Uuid`, `ExpectedRevision: long`, `RuleKind: string`, `Parameters: AccessExpectationParameters`, `Rationale: string`, `EffectiveFrom: DateTimeOffset`, `EffectiveUntil: DateTimeOffset?`, `SupersedesExpectationId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessExpectation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessExpectationProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessExpectationView","firstHttpStatus":200,"terminalResultType":"AccessExpectationView"}`.
- **Owner protocols:** Expectation succession and population exceptions.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ProposeAccessExpectation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ProposeAccessExpectation.cs).
- **Selected changes:** Add required ExpectationId. Rename ExpectedLedgerRevision to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessClassificationAndVarianceTests.cs`.

## ProposeControlCriterionMapping

- **Handler → first owner:** `ProposeControlCriterionMappingHandler` → `ControlCriterionMapping`.
- **Logical identity:** `{"field":"MappingId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}`; MCP disabled.
- **Exact fields:** `MappingId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `ControlVersionId: Uuid`, `EditionId: Uuid`, `CriterionIdentifier: string`, `ExpectedRevision: long`, `Rationale: string`, `ApplicabilityExplanation: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlCriterionMappingProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlCriterionMappingRegistration","firstHttpStatus":200,"terminalResultType":"ControlCriterionMappingRegistration"}`.
- **Owner protocols:** Per-tuple mapping succession.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/ProposeControlCriterionMapping.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/ProposeControlCriterionMapping.cs).
- **Selected changes:** Add required MappingId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`, `test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs`.

## ProposeControlOperatingPlan

- **Handler → first owner:** `ProposeControlOperatingPlanHandler` → `ControlOperatingPlan`.
- **Logical identity:** `{"field":"PlanVersionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}`; MCP enabled.
- **Exact fields:** `PlanVersionId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `ExpectedRevision: long`, `ControlVersionId: Uuid`, `Owner: OperatingHolder`, `BackupOwner: OperatingHolder?`, `ReviewerMemberId: Uuid`, `Cadence: ControlCadence`, `EffectiveFrom: DateOnly`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOperatingPlan","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOperatingPlanProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOperatingPlanView","firstHttpStatus":200,"terminalResultType":"ControlOperatingPlanView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/ProposeControlOperatingPlan.cs](../../../../src/Compliance.Common/Features/Operations/Commands/ProposeControlOperatingPlan.cs).
- **Selected changes:** Add required PlanVersionId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`.

## ProposeCriterionNotApplicable

- **Handler → first owner:** `ProposeCriterionNotApplicableHandler` → `CriterionApplicability`.
- **Logical identity:** `{"field":"DecisionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}`; MCP disabled.
- **Exact fields:** `DecisionId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `EditionId: Uuid`, `CriterionIdentifier: string`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `CriterionNotApplicableProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"CriterionApplicabilityRegistration","firstHttpStatus":200,"terminalResultType":"CriterionApplicabilityRegistration"}`.
- **Owner protocols:** Per-criterion applicability line.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/ProposeCriterionNotApplicable.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/ProposeCriterionNotApplicable.cs).
- **Selected changes:** Add required DecisionId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`, `test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`.

## ProposeRiskControlTreatment

- **Handler → first owner:** `ProposeRiskControlTreatmentHandler` → `RiskTreatmentAssertion`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ExpectedRevision: long`, `ControlId: Uuid`, `ControlVersionId: Uuid`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAssertion","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskControlTreatmentProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"RiskControlTreatmentRegistration","firstHttpStatus":200,"terminalResultType":"RiskControlTreatmentRegistration"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/ProposeRiskControlTreatment.cs](../../../../src/Compliance.Common/Features/Risks/Commands/ProposeRiskControlTreatment.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/AttestRiskManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`, `test/Compliance.Tests/Features/Risks/PersonalRiskSignoffTransportTests.cs`.

## ProposeServiceEngagementStaff

- **Handler → first owner:** `ProposeServiceEngagementStaffHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `StaffMemberId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementMutationRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/ProposeServiceEngagementStaff.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/ProposeServiceEngagementStaff.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/IndependenceAssignmentBoundaryTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## ProposeSystemDescriptionSuccessor

- **Handler → first owner:** `ProposeSystemDescriptionSuccessorHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/propose-system-description-successor/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `ExpectedRevision: long`, `ChangeReason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionSuccessorProposed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/ProposeSystemDescriptionSuccessor.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/ProposeSystemDescriptionSuccessor.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## RaiseFinding

- **Handler → first owner:** `RaiseFindingHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"FindingId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}`; MCP enabled.
- **Exact fields:** `FindingId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `Source: FindingSource`, `Title: string`, `Description: string`, `Severity: string`, `AffectedScope: string`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`, `Links: IReadOnlyList<FindingLink>?`.
- **Revision scope:** `{"field":null,"owner":"FindingAggregate","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `FindingRaisedV2`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FindingRegistration","firstHttpStatus":200,"terminalResultType":"FindingRegistration"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/RaiseFinding.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/RaiseFinding.cs).
- **Selected changes:** Add required FindingId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`.

## RaiseOccurrenceFinding

- **Handler → first owner:** `RaiseOccurrenceFindingHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"FindingId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ControlId: Uuid`, `OccurrenceId: Uuid`, `SourceVersion: string`, `SourceKind: string`, `SourceText: string`.
- **Revision scope:** `{"field":null,"owner":"FindingAggregate","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `FindingRaisedV2`, `CorrectiveActionAdmitted`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/RaiseOccurrenceFinding.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/RaiseOccurrenceFinding.cs).
- **Selected changes:** When source kind is occurrence_review, preserve the default requested corrective action by atomically admitting its complete deterministic ActionId/content alongside FindingRaisedV2; materialize it independently. Failed/skipped attestation variants without requested action emit FindingRaisedV2 only.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Remediation/OccurrenceFindingTests.cs`.

## RaiseRiskReassessmentTriggers

- **Handler → first owner:** `RaiseRiskReassessmentTriggersHandler` → `RiskGovernance`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `TriggerKind: string`, `SourceReference: string`, `RaisedAt: DateTimeOffset`, `MethodVersionId: Uuid?`, `RiskId: Uuid`.
- **Revision scope:** `{"field":null,"owner":"RiskGovernance","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `RiskReassessmentTriggered`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/RaiseRiskReassessmentTriggers.cs](../../../../src/Compliance.Common/Features/Risks/Commands/RaiseRiskReassessmentTriggers.cs).
- **Selected changes:** Add required CommandId. Retain public type name during direct cutover but process exactly one RiskId per source-derived internal command; reactor owns complete fan-out.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## ReassignAccessReviewResponsibility

- **Handler → first owner:** `ReassignAccessReviewResponsibilityHandler` → `AccessReviewItem`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ItemId: Uuid`, `Responsibility: string`, `ExpectedRevision: long`, `AssignedMemberId: Uuid`, `Reason: string`, `DelegationReason: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessReviewResponsibilityReassigned`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessReviewResponsibilityReassignmentView","firstHttpStatus":200,"terminalResultType":"AccessReviewResponsibilityReassignmentView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/ReassignAccessReviewResponsibility.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/ReassignAccessReviewResponsibility.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs`, `test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs`.

## ReconcileCampaignAudience

- **Handler → first owner:** `ReconcileCampaignAudienceHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `CampaignId: Uuid`, `RosterSnapshotId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"PolicyDistributionCampaign","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `PolicyCampaignAudienceAmended`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"CampaignReconciliationView","firstHttpStatus":200,"terminalResultType":"CampaignReconciliationView"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/ReconcileCampaignAudience.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/ReconcileCampaignAudience.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## RecordAccessDecision

- **Handler → first owner:** `RecordAccessDecisionHandler` → `AccessReviewItem`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ItemId: Uuid`, `ExpectedRevision: long`, `Decision: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessDecisionsRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessDecisionView","firstHttpStatus":200,"terminalResultType":"AccessDecisionView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/RecordAccessDecision.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessDecision.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs`, `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/RestrictedAccessReviewCampaignReadTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs`, `test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`.

## RecordAccessPopulationFacts

- **Handler → first owner:** `RecordAccessPopulationFactsHandler` → `AccessPopulation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-populations/record-access-population-facts/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `PopulationId: Uuid`, `ExpectedRevision: long`, `Principals: IReadOnlyList<AccessPrincipalFact>`, `Entitlements: IReadOnlyList<AccessEntitlementFact>`, `GroupMembers: IReadOnlyList<AccessGroupMemberFact>`, `Assignments: IReadOnlyList<AccessAssignmentFact>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessPopulation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessPopulationFactsRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessPopulationRegistration","firstHttpStatus":200,"terminalResultType":"AccessPopulationRegistration"}`.
- **Owner protocols:** Population acceptance and principal classification.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/RecordAccessPopulationFacts.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessPopulationFacts.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs`, `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessPopulationHandlerTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs`.

## RecordAccessRemediationChange

- **Handler → first owner:** `RecordAccessRemediationChangeHandler` → `AccessReviewItem`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ItemId: Uuid`, `ExpectedRevision: long`, `Reference: string`, `Description: string`, `ChangedAt: DateTimeOffset`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessRemediationChangeRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessRemediationChangeView","firstHttpStatus":200,"terminalResultType":"AccessRemediationChangeView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/RecordAccessRemediationChange.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/RecordAccessRemediationChange.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs`, `test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`.

## RecordAdvisorFeedback

- **Handler → first owner:** `RecordAdvisorFeedbackHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"FeedbackId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `FeedbackId: Uuid`, `RecordId: Uuid`, `RecordVersionId: Uuid`, `Kind: string`, `Body: string`, `ExpectedHandoffRevision: long`, `SelectedSource: AdvisorRecordVersion?`.
- **Revision scope:** `{"field":"ExpectedHandoffRevision","owner":"AdvisorHandoff","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `AdvisorFeedbackAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/RecordAdvisorFeedback.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RecordAdvisorFeedback.cs).
- **Selected changes:** Include init-only authored scope and field-presence semantics omitted by the earlier positional-record scan. JsonIgnore presence flags are adapter-owned and canonicalized explicitly; never serialize/replay them as if false meant omitted.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Readiness/AdvisoryReadinessNoteIndependenceTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## RecordApplicationRelationship

Remove this overlapping compatibility command/handler/route/MCP tool and ApplicationRelationshipRecorded emitter after canonical AddApplicationRelationship and all readers consume ApplicationRelationshipAdded. Preserve its active-endpoint, visibility and exact source/target revision checks in canonical Add; disposable pre-release histories need no old writer.

## RecordArtifactRetentionBasis

- **Handler → first owner:** `RecordArtifactRetentionBasisHandler` → `ArtifactRetentionBasis`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/basis/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `SourceKind: string`, `SourceId: Uuid`, `ExpectedContentSha256: string`, `ExpectedRevision: long`, `Reason: string`, `PeriodStart: DateOnly?`, `PeriodEnd: DateOnly?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ArtifactRetentionBasis","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ArtifactRetentionBasisRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Retention basis and authoritative legal-hold barrier.
- **Preserved source validation:** [src/Compliance.Common/Features/Retention/RecordArtifactRetentionBasis.cs](../../../../src/Compliance.Common/Features/Retention/Commands/RecordArtifactRetentionBasis.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs`, `test/Compliance.Tests/Features/Retention/AttestRetentionManagementWriteWallTests.cs`.

## RecordAssuranceReport

- **Handler → first owner:** `RecordAssuranceReportHandler` → `ProviderAssuranceReport`.
- **Logical identity:** `{"field":"ReportId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/{report_id}`; MCP enabled.
- **Exact fields:** `ReportId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `Content: AssuranceReportContent`.
- **Revision scope:** `{"field":null,"owner":"ProviderAssuranceReport","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AssuranceReportRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AssuranceReportRegistration","firstHttpStatus":200,"terminalResultType":"AssuranceReportRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/RecordAssuranceReport.cs](../../../../src/Compliance.Common/Features/Providers/Commands/RecordAssuranceReport.cs).
- **Selected changes:** Add required ReportId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs`, `test/Compliance.Tests/Features/Providers/ProviderAssurancePipelineTests.cs`.

## RecordAuditPackageBuildResult

- **Handler → first owner:** `RecordAuditPackageBuildResultHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"UUIDv5(source eventId, length-prefixed effectKind + canonical target stream + source lifecycle episode); external process callback source is the immutable accepted business source event plus exact effect key, never attemptId."}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `Source: SourceFactProof<AuditPackageBuildRequested>`, `Target: EventStreamAddress`, `OperationId: Uuid`, `Generation: long`, `ExpectedRevision: long?`, `Intent: ImmutableContentRef`, `PackageId: Uuid`, `BuildId: Uuid`, `Output: AuditPackageBuildOutput`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":"Explicit null requires exact source authorization/generation and physical append OCC; never substitute current logical revision."}`.
- **Native facts:** `AuditPackageBuilt`.
- **Resolved intent:** `PackageBuildIntent`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/RecordAuditPackageBuildResult.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/RecordAuditPackageBuildResult.cs).
- **Selected changes:** Add required CommandId. Use the exact trusted internal receiver signature from the reactor catalog; Source is concretely typed and carries the accepted business fact, never a process-stage pseudo-event.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/RecordAuditPackageBuildResultHandlerTests.cs`.

## RecordAuditPackageDelivery

- **Handler → first owner:** `RecordAuditPackageDeliveryHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"DeliveryId","type":"Uuid","required":true,"origin":"client","rule":"Must equal existing UUIDv5(53cb20d6-01ef-5fd5-99b3-04087e39bf77, packageId + newline + packageVersion + newline + Clean(recipient) + newline + Clean(deliveryReference)); this identifies exact manual delivery recording."}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/deliveries/{delivery_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `PackageVersion: long`, `Recipient: string`, `DeliveredAt: DateTimeOffset`, `DeliveryReference: string`, `DeliveryId: Uuid`, `ExpectedManifestRevision: long`.
- **Revision scope:** `{"field":"ExpectedManifestRevision","owner":"AuditPackageManifest","meaning":"current package manifest owner revision at personal recording admission"}`.
- **Native facts:** `AuditPackageDeliveryAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/RecordAuditPackageDelivery.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/RecordAuditPackageDelivery.cs).
- **Selected changes:** Retain personal HTTP actor and authored DeliveredAt/DeliveryReference. Verify deterministic DeliveryId and current exact archive/manifest/validation/recipient approval before admission. Freeze delivered version at this admission. Child AuditPackageDelivery materializes the accepted record; no automated send or provider callback is introduced.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs`.

## RecordAuditorRequest

- **Handler → first owner:** `RecordAuditorRequestHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"RequestId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ExaminationId: Uuid`, `Source: string`, `ExternalReference: string`, `Wording: string`, `Scope: string`, `Priority: string`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`.
- **Revision scope:** `{"field":null,"owner":"AuditorRequest","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `AuditorRequestRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequest.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/RecordAuditorRequest.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## RecordAuditorRequestComment

- **Handler → first owner:** `RecordAuditorRequestCommentHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"CommentId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request-comment/{comment_id}`; MCP disabled.
- **Exact fields:** `CommentId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `Body: string`, `Compartment: string`, `ExpectedRequestRevision: long`.
- **Revision scope:** `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `AuditorCommentAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequestComment.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/RecordAuditorRequestComment.cs).
- **Selected changes:** Add required CommentId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## RecordAuditorResponseDisposition

- **Handler → first owner:** `RecordAuditorResponseDispositionHandler` → `AuditorResponse`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/record-auditor-response-disposition/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `Version: long`, `Disposition: string`, `ExternalReference: string?`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorResponseDispositionRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/RecordAuditorResponseDisposition.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/RecordAuditorResponseDisposition.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## RecordBulkAccessDecision

- **Handler → first owner:** `RecordBulkAccessDecisionHandler` → `AccessReviewCampaign`.
- **Logical identity:** `{"field":"DecisionBatchId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions/{decision_batch_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `CampaignId: Uuid`, `ItemIds: IReadOnlyList<Uuid>`, `Decision: string`, `Rationale: string`, `PreviewToken: string`, `ExpectedCampaignRevision: long`, `DecisionBatchId: Uuid`.
- **Revision scope:** `{"field":"ExpectedCampaignRevision","owner":"AccessReviewCampaign","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `AccessReviewDecisionBatchAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"BulkAccessDecisionResult"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/RecordBulkAccessDecision.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/RecordBulkAccessDecision.cs).
- **Selected changes:** Add required DecisionBatchId. Add required owner-local ExpectedCampaignRevision; business Version remains an exact content version, not owner OCC. Admit complete batch intent on the campaign before child creation, so completion observes every admitted bulk operation. Preview binds exact selected item heads; child batch starts at revision zero.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AttestAccessReviewWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessReviews/AccessReviewCampaignTests.cs`.

## RecordControlEvaluationStep

- **Handler → first owner:** `RecordControlEvaluationStepHandler` → `ControlEvaluation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `EvaluationId: Uuid`, `StepId: Uuid`, `ExpectedRevision: long`, `Result: string`, `Rationale: string`, `InspectedItems: IReadOnlyList<EvaluationInspectedItem>`, `DeviationClassification: string?`, `DeviationDescription: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlEvaluationStepRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}`.
- **Owner protocols:** Independent evaluation and retest.
- **Preserved source validation:** [src/Compliance.Common/Features/Evaluations/RecordControlEvaluationStep.cs](../../../../src/Compliance.Common/Features/Evaluations/Commands/RecordControlEvaluationStep.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs`, `test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs`, `test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## RecordDirectoryIndependenceReevaluation

- **Handler → first owner:** `RecordDirectoryIndependenceReevaluationHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"source_event_effect","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** internal_trusted_command; Trusted internal/process entry only.; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `DirectoryResourceOffset: ulong`, `DirectoryEventId: Uuid`, `DirectoryPayloadSha256: string`, `AcceptanceResourceOffset: ulong`, `AcceptanceEventId: Uuid`, `AcceptanceRequestId: Uuid`, `AcceptancePayloadSha256: string`, `StaffMemberId: Uuid`, `UserId: Uuid`, `EngagementId: Uuid`, `AcceptedServiceEpoch: long`.
- **Revision scope:** `{"field":null,"owner":"ServiceEngagementIndependence","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `DirectoryIndependenceReevaluated`.
- **Original response:** `{"mode":"internal","responseType":"Unit","firstHttpStatus":null,"terminalResultType":"Unit"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs](../../../../src/Compliance.Core/Features/AccessControl/Models/RecordDirectoryIndependenceReevaluation.cs).
- **Selected changes:** Add required CommandId. Target one engagement acceptance pair; discovery/fan-out belongs to the reactor, not a tenant-wide mutation.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/DirectoryIndependenceReevaluationTests.cs`.

## RecordFirmProfessionalDutyDesignation

- **Handler → first owner:** `RecordFirmProfessionalDutyDesignationHandler` → `FirmProfessionalDutyDesignation`.
- **Logical identity:** `{"field":"AssertionId","type":"Uuid","required":true,"origin":"client","rule":"one immutable signed authority assertion; verified AssertionId must match the signed assertion"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/platform/firm-professional-duties/{designation_id}/assertions/{assertion_id}`; MCP disabled.
- **Exact fields:** `AssertionId: Uuid`, `DesignationId: Uuid`, `SignedAssertion: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FirmProfessionalDutyDesignation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ProfessionalDutyChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FirmProfessionalDutyDesignationView"}`.
- **Owner protocols:** Signed assertion claims and duty-scope designation.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RecordFirmProfessionalDutyDesignation.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RecordFirmProfessionalDutyDesignation.cs).
- **Selected changes:** Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. AssertionId is required and must equal the verified signed assertion identity; DesignationId cannot identify repeated refresh/revocation assertions.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/ProfessionalDutyGovernanceCompositionTests.cs`.

## RecordLocation

- **Handler → first owner:** `RecordLocationHandler` → `Location`.
- **Logical identity:** `{"field":"LocationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/locations/{location_id}`; MCP enabled.
- **Exact fields:** `LocationId: Uuid`, `TenantId: Uuid`, `Kind: string`, `Name: string`, `OwnerPersonId: Uuid`, `GeographyReference: string?`.
- **Revision scope:** `{"field":null,"owner":"Location","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `LocationChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"LocationRegistration"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/TechnologyInventory/RecordLocation.cs](../../../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordLocation.cs).
- **Selected changes:** Add required LocationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## RecordNonattestService

- **Handler → first owner:** `RecordNonattestServiceHandler` → `ClientServiceIndependence`.
- **Logical identity:** `{"field":"ServiceRecordId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id}`; MCP enabled.
- **Exact fields:** `TenantId: Uuid`, `ServiceRecordId: Uuid`, `ExpectedServiceEpoch: long`, `Content: NonattestServiceContent`.
- **Revision scope:** `{"field":"ExpectedServiceEpoch","owner":"ClientServiceIndependence","meaning":"complete service history epoch only; evaluation/read-use records do not advance it"}`.
- **Native facts:** `NonattestServiceRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"NonattestServiceView","firstHttpStatus":200,"terminalResultType":"NonattestServiceView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RecordNonattestService.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RecordNonattestService.cs).
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/SourceIndependenceReevaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## RecordOperationalProcess

- **Handler → first owner:** `RecordOperationalProcessHandler` → `OperationalProcess`.
- **Logical identity:** `{"field":"OperationalProcessId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}`; MCP enabled.
- **Exact fields:** `OperationalProcessId: Uuid`, `TenantId: Uuid`, `Name: string`, `Purpose: string`, `OwnerPersonId: Uuid`, `Inputs: string?`, `Outputs: string?`.
- **Revision scope:** `{"field":null,"owner":"OperationalProcess","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `OperationalProcessChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"OperationalProcessRegistration"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/TechnologyInventory/RecordOperationalProcess.cs](../../../../src/Compliance.Common/Features/TechnologyInventory/Commands/RecordOperationalProcess.cs).
- **Selected changes:** Add required OperationalProcessId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## RecordPartnerIndependenceEvaluation

- **Handler → first owner:** `RecordPartnerIndependenceEvaluationHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"EvaluationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/partner-evaluations/{evaluation_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `EngagementId: Uuid`, `EvaluationId: Uuid`, `ExpectedEngagementRevision: long`, `RuleVersion: long`, `ExpectedRuleContentDigest: string`, `ExpectedServiceHistoryDigest: string`, `Rationale: string`, `ExpectedServiceEpoch: long`.
- **Revision scope:** `{"field":"ExpectedEngagementRevision","owner":"ServiceEngagementIndependence","meaning":"current engagement content revision; ExpectedServiceEpoch separately binds complete client history"}`.
- **Native facts:** `EngagementPersonalDecisionAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"PartnerIndependenceEvaluationView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RecordPartnerIndependenceEvaluation.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RecordPartnerIndependenceEvaluation.cs).
- **Selected changes:** Drop obsolete tenant ExpectedSequence/ExpectedRevision. Admit personal decision against ExpectedEngagementRevision and ExpectedServiceEpoch, pin engagement content, then materialize after exact source uses. Personal records remain independently owned; admission alone does not approve production acceptance.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/PartnerIndependenceEvaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/ServiceEngagementAcceptanceTests.cs`, `test/Compliance.Tests/Features/AccessControl/SourceIndependenceReevaluationTests.cs`.

## RecordProvider

- **Handler → first owner:** `RecordProviderHandler` → `Provider`.
- **Logical identity:** `{"field":"ProviderId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}`; MCP enabled.
- **Exact fields:** `ProviderId: Uuid`, `TenantId: Uuid`, `Content: ProviderContent`.
- **Revision scope:** `{"field":null,"owner":"Provider","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ProviderChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderRegistration"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/RecordProvider.cs](../../../../src/Compliance.Common/Features/Providers/Commands/RecordProvider.cs).
- **Selected changes:** Add required ProviderId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs`, `test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs`, `test/Compliance.Tests/Features/Providers/AttestProviderManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Providers/ProviderWriteAuthorizationProbeHandler.cs`, `test/Compliance.Tests/Features/Providers/ProviderAuthorizationTests.cs`.

## RecordProviderCoverageGap

- **Handler → first owner:** `RecordProviderCoverageGapHandler` → `ProviderAssuranceGap`.
- **Logical identity:** `{"field":"GapId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}`; MCP enabled.
- **Exact fields:** `GapId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `Content: ProviderCoverageGapContent`.
- **Revision scope:** `{"field":null,"owner":"ProviderAssuranceGap","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ProviderCoverageGapRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ProviderCoverageGapRegistration","firstHttpStatus":200,"terminalResultType":"ProviderCoverageGapRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/RecordProviderCoverageGap.cs](../../../../src/Compliance.Common/Features/Providers/Commands/RecordProviderCoverageGap.cs).
- **Selected changes:** Add required GapId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## RecordProviderReview

- **Handler → first owner:** `RecordProviderReviewHandler` → `ProviderAssuranceReview`.
- **Logical identity:** `{"field":"ReviewId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews/{review_id}`; MCP disabled.
- **Exact fields:** `ReviewId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `Content: ProviderReviewContent`.
- **Revision scope:** `{"field":null,"owner":"ProviderAssuranceReview","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ProviderReviewRequested`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderReviewRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/RecordProviderReview.cs](../../../../src/Compliance.Common/Features/Providers/Commands/RecordProviderReview.cs).
- **Selected changes:** Add required ReviewId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs`, `test/Compliance.Tests/Features/Providers/ProviderAssurancePipelineTests.cs`.

## RecordSampleSelection

- **Handler → first owner:** `RecordSampleSelectionHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"SelectionId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/record-sample-selection/{selection_id}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `ExaminationId: Uuid`, `AuditorRequestId: Uuid`, `ExpectedRequestRevision: long`, `SelectionId: Uuid`, `PopulationId: Uuid`, `PopulationVersion: int`, `PopulationRowIds: IReadOnlyList<Uuid>`, `SelectionMethod: string`, `Source: string`, `ExternalReference: string`.
- **Revision scope:** `{"field":"ExpectedRequestRevision","owner":"AuditorRequest","meaning":"open request admission revision, not new selection revision zero"}`.
- **Native facts:** `AuditorSampleSelectionAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/RecordSampleSelection.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/RecordSampleSelection.cs).
- **Selected changes:** Admit exact immutable selection on the open request before materializing child. Amendment keeps prior population identity and strictly later population version. Remove the accidental comparison of request revision to child zero.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestTests.cs`, `test/Compliance.Tests/Features/AuditorRequests/SampleSelectionCommandHandlerTests.cs`.

## RecordSystemDescriptionNoChangeDecision

- **Handler → first owner:** `RecordSystemDescriptionNoChangeDecisionHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/record-system-description-no-change-decision/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `Version: long`, `ChangeId: Uuid`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionNoChangeRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/RecordSystemDescriptionNoChangeDecision.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/RecordSystemDescriptionNoChangeDecision.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## RecordTrainingCompletion

- **Handler → first owner:** `RecordTrainingCompletionHandler` → `PolicyDistributionCampaign`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `CampaignId: Uuid`, `PersonId: Uuid`, `RequirementVersion: long`, `CompletedOn: DateOnly`, `Source: string`, `EvidenceReference: string`, `ExpectedCampaignRevision: long`.
- **Revision scope:** `{"field":"ExpectedCampaignRevision","owner":"PolicyDistributionCampaign","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `PolicyParticipantResultAdmitted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"CampaignCompletionView"}`.
- **Owner protocols:** Participant admission and campaign closure.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/RecordTrainingCompletion.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/RecordTrainingCompletion.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedCampaignRevision; business Version remains an exact content version, not owner OCC. Admit complete participant result on the open campaign generation first; apply to participant through committed admission fact. Personal source actor is retained.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/TrainingWorkAuthorityTests.cs`.

## RegisterFirmStaff

- **Handler → first owner:** `RegisterFirmStaffHandler` → `FirmStaffRecord`.
- **Logical identity:** `{"field":"StaffMemberId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/platform/firm-staff/{staff_member_id}/register-firm-staff`; MCP enabled.
- **Exact fields:** `StaffMemberId: Uuid`, `UserId: Uuid`, `Practice: string`, `SourceReference: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FirmStaffRecord","meaning":"new independent owner creation requires zero; former directory/tenant/system ledger revision is not reused"}`.
- **Native facts:** `FirmStaffRegistrationAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FirmStaffMemberView"}`.
- **Owner protocols:** Permanent staff identity mapping.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RegisterFirmStaff.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RegisterFirmStaff.cs).
- **Selected changes:** Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/ServiceEngagementHandlerTests.cs`, `test/Compliance.Tests/Features/AccessControl/ProfessionalDutyGovernanceCompositionTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## ReleaseArtifactLegalHold

- **Handler → first owner:** `ReleaseArtifactLegalHoldHandler` → `ArtifactLegalHold`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `SourceKind: string`, `SourceId: Uuid`, `ExpectedContentSha256: string`, `HoldId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ArtifactLegalHold","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ArtifactLegalHoldReleased`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Retention basis and authoritative legal-hold barrier.
- **Preserved source validation:** [src/Compliance.Common/Features/Retention/ReleaseArtifactLegalHold.cs](../../../../src/Compliance.Common/Features/Retention/Commands/ReleaseArtifactLegalHold.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs`, `test/Compliance.Tests/Features/Retention/AttestRetentionManagementWriteWallTests.cs`.

## RemoveApplicationRelationship

- **Handler → first owner:** `RemoveApplicationRelationshipHandler` → `ApplicationRelationship`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}/removals/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `RelationshipId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ApplicationRelationship","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationRelationshipRemoved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ApplicationRelationshipChange","firstHttpStatus":200,"terminalResultType":"ApplicationRelationshipChange"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/Commands/RemoveApplicationRelationship.cs](../../../../src/Compliance.Common/Features/Applications/Commands/RemoveApplicationRelationship.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedRelationshipRevision to ExpectedRevision and compare the new target owner revision. Canonical sole relationship removal command; preserve all endpoint/type/authority and reason rules from removed governed-removal path. Require Reason. Use owner-local ExpectedRevision, not a directory visible revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRelationshipRequestTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRelationshipTests.cs`.

## RemoveGovernedApplicationRelationship

Remove overlapping governed-removal compatibility handler/route/tool and ApplicationRelationshipGovernedRemoved writer. Canonical RemoveApplicationRelationship retains its source/target/type and required reason/authority guards; all consumers use ApplicationRelationshipRemoved after disposable-history cutover.

## ReopenAuditorRequest

- **Handler → first owner:** `ReopenAuditorRequestHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/reopen-auditor-request/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ReopenReason: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorRequestReopened`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/ReopenAuditorRequest.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/ReopenAuditorRequest.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## ReopenFinding

- **Handler → first owner:** `ReopenFindingHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `FindingReopenedV2`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/ReopenFinding.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/ReopenFinding.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## RequestAuditPackageBuild

- **Handler → first owner:** `RequestAuditPackageBuildHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"BuildKey","type":"string","required":true,"origin":"client","rule":"Required BuildKey with PackageId/PackageVersion. IIdentifiedRequest.RequestId = UUIDv5(698c0bb6-2dfc-58b3-b2fd-2f3dc04369f8, $\"{PackageId}\\n{PackageVersion}\\n{BuildKey.Trim()}\"); preserve existing token validation."}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/builds/{build_key}`; MCP disabled.
- **Exact fields:** `TenantId: Uuid`, `ProgramId: Uuid`, `ExaminationId: Uuid`, `PackageId: Uuid`, `PackageKind: string`, `BuildKey: string`, `ExpectedManifestRevision: long`, `PackageVersion: long`.
- **Revision scope:** `{"field":"ExpectedManifestRevision","owner":"AuditPackageManifest","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `AuditPackageBuildRequested`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/RequestAuditPackageBuild.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/RequestAuditPackageBuild.cs).
- **Selected changes:** Add required owner-local ExpectedManifestRevision; business Version remains an exact content version, not owner OCC. Freeze BuildId derived by the existing BuildNamespace algorithm, exact manifest version/reference/selection and original actor in PackageBuildIntent before starting worker. Technical AttemptId is a separate process field.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/AuditPackageCommandHandlerTests.cs`.

## ResolveAdvisorFeedback

- **Handler → first owner:** `ResolveAdvisorFeedbackHandler` → `AdvisorFeedbackThread`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `FeedbackId: Uuid`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorFeedbackResolved`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/ResolveAdvisorFeedback.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/ResolveAdvisorFeedback.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## ResolveAuditorRequest

- **Handler → first owner:** `ResolveAuditorRequestHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/resolve-auditor-request/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `Resolution: string`, `ExpectedRevision: long`, `ResponseIds: IReadOnlyList<Uuid>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorRequestResolutionAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/ResolveAuditorRequest.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/ResolveAuditorRequest.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC. ResponseIds must be nonempty, duplicate-free and exactly equal to every admitted response in this request resolution universe; freeze complete selection at admission.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`, `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## RespondToAdvisorFeedback

- **Handler → first owner:** `RespondToAdvisorFeedbackHandler` → `AdvisorFeedbackThread`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `FeedbackId: Uuid`, `Response: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorFeedbackThread","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorFeedbackResponded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/RespondToAdvisorFeedback.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RespondToAdvisorFeedback.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## RetireApplication

- **Handler → first owner:** `RetireApplicationHandler` → `DeclaredApplication`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/applications/{application_id}/retirements/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `ExpectedRevision: long`, `EffectiveAt: DateTimeOffset`, `Reason: string`, `MergedIntoApplicationId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationRetired`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/RetireApplication.cs](../../../../src/Compliance.Common/Features/Applications/Commands/RetireApplication.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs`.

## RetireControlCriterionMapping

- **Handler → first owner:** `RetireControlCriterionMappingHandler` → `ControlCriterionMapping`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `MappingId: Uuid`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlCriterionMappingRetired`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Per-tuple mapping succession.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/RetireControlCriterionMapping.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/RetireControlCriterionMapping.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`.

## RetireRiskControlTreatment

- **Handler → first owner:** `RetireRiskControlTreatmentHandler` → `RiskTreatmentAssertion`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `TreatmentId: Uuid`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAssertion","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskControlTreatmentRetired`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/RetireRiskControlTreatment.cs](../../../../src/Compliance.Common/Features/Risks/Commands/RetireRiskControlTreatment.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## ReviewAuditResponse

- **Handler → first owner:** `ReviewAuditResponseHandler` → `AuditorResponse`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/review-audit-response/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `Version: long`, `Outcome: string`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorResponseDisclosureReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/ReviewAuditResponse.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/ReviewAuditResponse.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## ReviewControlCriterionMapping

- **Handler → first owner:** `ReviewControlCriterionMappingHandler` → `ControlCriterionMapping`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `MappingId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlCriterionMapping","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlCriterionMappingReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Per-tuple mapping succession.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/ReviewControlCriterionMapping.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/ReviewControlCriterionMapping.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`, `test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## ReviewControlEvaluation

- **Handler → first owner:** `ReviewControlEvaluationHandler` → `ControlEvaluation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `EvaluationId: Uuid`, `ExpectedRevision: long`, `Decision: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlEvaluationReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}`.
- **Owner protocols:** Independent evaluation and retest.
- **Preserved source validation:** [src/Compliance.Common/Features/Evaluations/ReviewControlEvaluation.cs](../../../../src/Compliance.Common/Features/Evaluations/Commands/ReviewControlEvaluation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`, `test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## ReviewControlOccurrence

- **Handler → first owner:** `ReviewControlOccurrenceHandler` → `ControlOccurrence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `OccurrenceId: Uuid`, `ExpectedRevision: long`, `AttestationId: Uuid`, `Outcome: string`, `Rationale: string`, `RequestedActions: IReadOnlyList<string>?`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlOccurrence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlOccurrenceReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlOccurrenceView","firstHttpStatus":200,"terminalResultType":"ControlOccurrenceView"}`.
- **Owner protocols:** Operating plan and business-window occurrence.
- **Preserved source validation:** [src/Compliance.Common/Features/Operations/ReviewControlOccurrence.cs](../../../../src/Compliance.Common/Features/Operations/Commands/ReviewControlOccurrence.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`.

## ReviewCriterionApplicability

- **Handler → first owner:** `ReviewCriterionApplicabilityHandler` → `CriterionApplicability`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DecisionId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `CriterionApplicabilityReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Per-criterion applicability line.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/ReviewCriterionApplicability.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/ReviewCriterionApplicability.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`, `test/Compliance.Tests/Features/ControlMappings/PersonalMappingDecisionTransportTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## ReviewRiskControlTreatment

- **Handler → first owner:** `ReviewRiskControlTreatmentHandler` → `RiskTreatmentAssertion`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `TreatmentId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAssertion","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskControlTreatmentReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/ReviewRiskControlTreatment.cs](../../../../src/Compliance.Common/Features/Risks/Commands/ReviewRiskControlTreatment.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`, `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## ReviewRiskTreatmentActionCompletion

- **Handler → first owner:** `ReviewRiskTreatmentActionCompletionHandler` → `RiskTreatmentAction`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ActionId: Uuid`, `ExpectedRevision: long`, `Outcome: string`, `Rationale: string`, `SeparationOfDutiesWaiverId: Uuid?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskTreatmentActionCompletionReviewAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/ReviewRiskTreatmentActionCompletion.cs](../../../../src/Compliance.Common/Features/Risks/Commands/ReviewRiskTreatmentActionCompletion.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`, `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## ReviewSystemDescription

- **Handler → first owner:** `ReviewSystemDescriptionHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/review-system-description/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `Version: long`, `Outcome: string`, `Comments: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionReviewed`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/ReviewSystemDescription.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/ReviewSystemDescription.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionCommandHandlerTests.cs`.

## ReviseAdvisorHandoffSelection

- **Handler → first owner:** `ReviseAdvisorHandoffSelectionHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/selection-revisions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `ExpectedRevision: long`, `RecordVersions: IReadOnlyList<AdvisorRecordVersion>`, `ChangeReason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorHandoff","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorHandoffSelectionRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/ReviseAdvisorHandoffSelection.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/ReviseAdvisorHandoffSelection.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Controls/ControlDraftAuthorizationTests.cs`, `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## ReviseApplication

- **Handler → first owner:** `ReviseApplicationHandler` → `DeclaredApplication`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/applications/revise-application/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ApplicationId: Uuid`, `ExpectedRevision: long`, `Name: string`, `Purpose: string`, `OwnerReference: string?`, `Classification: string?`, `SystemOwnerPersonId: Uuid?`, `AccessOwnerPersonId: Uuid?`, `IsRestricted: bool?`, `ApplicationKind: string?`, `Criticality: string?`, `ClassificationIntent: ApplicationRevisionField` (adapter presence; queue DTO required), `SystemOwnerIntent: ApplicationRevisionField` (adapter presence; queue DTO required), `AccessOwnerIntent: ApplicationRevisionField` (adapter presence; queue DTO required).
- **Revision scope:** `{"field":"ExpectedRevision","owner":"DeclaredApplication","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ApplicationRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Application identity, relationships and import holds.
- **Preserved source validation:** [src/Compliance.Common/Features/Applications/ReviseApplication.cs](../../../../src/Compliance.Common/Features/Applications/Commands/ReviseApplication.cs).
- **Selected changes:** Add required CommandId. Include init-only authored scope and field-presence semantics omitted by the earlier positional-record scan. JsonIgnore presence flags are adapter-owned and canonicalized explicitly; never serialize/replay them as if false meant omitted.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRevisionFieldPresenceHttpTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRevisionPreviewContractTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationRevisionGovernanceTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationOwnerTests.cs`, `test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs`.

## ReviseAssuranceReport

- **Handler → first owner:** `ReviseAssuranceReportHandler` → `ProviderAssuranceReport`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/revise-assurance-report/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `ReportId: Uuid`, `ExpectedRevision: long`, `Content: AssuranceReportContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ProviderAssuranceReport","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AssuranceReportRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AssuranceReportRegistration","firstHttpStatus":200,"terminalResultType":"AssuranceReportRegistration"}`.
- **Owner protocols:** Independent assurance facts and exact source proofs.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/ReviseAssuranceReport.cs](../../../../src/Compliance.Common/Features/Providers/Commands/ReviseAssuranceReport.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs`, `test/Compliance.Tests/Features/Providers/ProviderAssurancePipelineTests.cs`.

## ReviseAuditResponseDraft

- **Handler → first owner:** `ReviseAuditResponseDraftHandler` → `AuditorResponse`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/revise-audit-response-draft/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `ExpectedRevision: long`, `Summary: string`, `EvidenceArtifactIds: IReadOnlyList<Uuid>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditResponseDraftRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/ReviseAuditResponseDraft.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/ReviseAuditResponseDraft.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## ReviseAuditorRequest

- **Handler → first owner:** `ReviseAuditorRequestHandler` → `AuditorRequest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/revise-auditor-request/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ExpectedRevision: long`, `Wording: string`, `Scope: string`, `Priority: string`, `DueOn: DateOnly`, `ChangeReason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorRequest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorRequestRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/ReviseAuditorRequest.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/ReviseAuditorRequest.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorRequestCommandHandlerTests.cs`.

## ReviseFinding

- **Handler → first owner:** `ReviseFindingHandler` → `FindingAggregate`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `FindingId: Uuid`, `ExpectedRevision: long`, `Severity: string`, `OwnerMemberId: Uuid`, `DueOn: DateOnly`, `AffectedScope: string`, `RootCause: string?`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FindingAggregate","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `FindingRevisedV2`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FindingView","firstHttpStatus":200,"terminalResultType":"FindingView"}`.
- **Owner protocols:** Action admission and finding closure.
- **Preserved source validation:** [src/Compliance.Common/Features/Remediation/ReviseFinding.cs](../../../../src/Compliance.Common/Features/Remediation/Commands/ReviseFinding.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`, `test/Compliance.Tests/Features/Remediation/FindingTests.cs`.

## ReviseLocation

- **Handler → first owner:** `ReviseLocationHandler` → `Location`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/locations/revise-location/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `LocationId: Uuid`, `ExpectedRevision: long`, `Name: string`, `OwnerPersonId: Uuid`, `Lifecycle: string`, `GeographyReference: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"Location","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `LocationChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/TechnologyInventory/ReviseLocation.cs](../../../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseLocation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## ReviseOperationalProcess

- **Handler → first owner:** `ReviseOperationalProcessHandler` → `OperationalProcess`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/operational-processes/revise-operational-process/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `OperationalProcessId: Uuid`, `ExpectedRevision: long`, `Name: string`, `Purpose: string`, `OwnerPersonId: Uuid`, `Lifecycle: string`, `Inputs: string?`, `Outputs: string?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"OperationalProcess","counter":"named process record revision","nullableInternalRule":null}`.
- **Native facts:** `OperationalProcessChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/TechnologyInventory/ReviseOperationalProcess.cs](../../../../src/Compliance.Common/Features/TechnologyInventory/Commands/ReviseOperationalProcess.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## ReviseProvider

- **Handler → first owner:** `ReviseProviderHandler` → `Provider`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/providers/revise-provider/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProviderId: Uuid`, `ExpectedRevision: long`, `Content: ProviderContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"Provider","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ProviderChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ProviderRegistration"}`.
- **Owner protocols:** Name claims and record lifecycle.
- **Preserved source validation:** [src/Compliance.Common/Features/Providers/ReviseProvider.cs](../../../../src/Compliance.Common/Features/Providers/Commands/ReviseProvider.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs`, `test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs`, `test/Compliance.Tests/Features/Providers/AttestProviderManagementWriteWallTests.cs`.

## ReviseRiskTreatmentAction

- **Handler → first owner:** `ReviseRiskTreatmentActionHandler` → `RiskTreatmentAction`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/revise-risk-treatment-action/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ActionId: Uuid`, `ExpectedRevision: long`, `Title: string`, `TargetState: string`, `ExpectedEvidence: string`, `DueOn: DateOnly`, `AccountableMemberId: Uuid`, `EvidenceRequestIds: IReadOnlyList<Uuid>?`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskTreatmentActionRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/ReviseRiskTreatmentAction.cs](../../../../src/Compliance.Common/Features/Risks/Commands/ReviseRiskTreatmentAction.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## ReviseSystemDescriptionDraft

- **Handler → first owner:** `ReviseSystemDescriptionDraftHandler` → `SystemDescription`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/revise-system-description-draft/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DescriptionId: Uuid`, `ExpectedRevision: long`, `Title: string`, `Sections: IReadOnlyList<SystemDescriptionSection>`, `ChangeReason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"SystemDescription","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `SystemDescriptionDraftRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Versioned description and independent export.
- **Preserved source validation:** [src/Compliance.Common/Features/SystemDescription/ReviseSystemDescriptionDraft.cs](../../../../src/Compliance.Common/Features/SystemDescription/Commands/ReviseSystemDescriptionDraft.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/SystemDescription/SystemDescriptionDraftCommandHandlerTests.cs`.

## ReviseTrainingRequirement

- **Handler → first owner:** `ReviseTrainingRequirementHandler` → `TrainingRequirement`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/revise-training-requirement/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequirementId: Uuid`, `ExpectedVersion: long`, `Content: TrainingRequirementContent`.
- **Revision scope:** `{"field":"ExpectedVersion","owner":"TrainingRequirement","meaning":"this owner has one immutable defined content version per business revision"}`.
- **Native facts:** `TrainingRequirementDefined`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"TrainingRequirementRegistration","firstHttpStatus":200,"terminalResultType":"TrainingRequirementRegistration"}`.
- **Owner protocols:** Independent training requirement.
- **Preserved source validation:** [src/Compliance.Common/Features/PolicyDistribution/ReviseTrainingRequirement.cs](../../../../src/Compliance.Common/Features/PolicyDistribution/Commands/ReviseTrainingRequirement.cs).
- **Selected changes:** Add required CommandId. Keep ExpectedVersion as the sole logical concurrency counter; do not require a redundant ExpectedRevision with the same meaning.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: .

## RevokeAdvisorHandoff

- **Handler → first owner:** `RevokeAdvisorHandoffHandler` → `AdvisorHandoff`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/revocations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `HandoffId: Uuid`, `Rationale: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AdvisorHandoff","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AdvisorHandoffRevoked`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Feedback admission versus handoff revocation.
- **Preserved source validation:** [src/Compliance.Common/Features/AdvisorCollaboration/RevokeAdvisorHandoff.cs](../../../../src/Compliance.Common/Features/AdvisorCollaboration/Commands/RevokeAdvisorHandoff.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AdvisorCollaboration/AdvisorHandoffCommandHandlerTests.cs`.

## RevokeFirmProfessionalDutyDesignation

- **Handler → first owner:** `RevokeFirmProfessionalDutyDesignationHandler` → `FirmProfessionalDutyDesignation`.
- **Logical identity:** `{"field":"AssertionId","type":"Uuid","required":true,"origin":"client","rule":"one immutable signed authority assertion; verified AssertionId must match the signed assertion"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/platform/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id}`; MCP disabled.
- **Exact fields:** `AssertionId: Uuid`, `DesignationId: Uuid`, `SignedAssertion: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FirmProfessionalDutyDesignation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ProfessionalDutyChangeAccepted`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"FirmProfessionalDutyDesignationView"}`.
- **Owner protocols:** Signed assertion claims and duty-scope designation.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RevokeFirmProfessionalDutyDesignation.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RevokeFirmProfessionalDutyDesignation.cs).
- **Selected changes:** Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision. AssertionId is required and must equal the verified signed assertion identity; DesignationId cannot identify repeated refresh/revocation assertions.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/ProfessionalDutyGovernanceCompositionTests.cs`.

## RevokeServiceEngagementActualStaff

- **Handler → first owner:** `RevokeServiceEngagementActualStaffHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `StaffMemberId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementAssignmentRevoked`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementAcceptanceView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementAcceptanceView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/RevokeServiceEngagementActualStaff.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/RevokeServiceEngagementActualStaff.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`.

## RunReadinessAssessment

- **Handler → first owner:** `RunReadinessAssessmentHandler` → `ProgramReadiness`.
- **Logical identity:** `{"field":"AssessmentId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}`; MCP disabled.
- **Exact fields:** `AssessmentId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ExpectedProgramRevision: long`, `AsOf: DateTimeOffset?`.
- **Revision scope:** `{"field":"ExpectedProgramRevision","owner":"ProgramReadiness","meaning":"revision of the public first-admission owner; never a new child owner revision"}`.
- **Native facts:** `ReadinessAssessmentRequested`.
- **Original response:** `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"ReadinessAssessmentRegistration"}`.
- **Owner protocols:** Latest readiness and gap-plan decision seal.
- **Preserved source validation:** [src/Compliance.Common/Features/Readiness/RunReadinessAssessment.cs](../../../../src/Compliance.Common/Features/Readiness/Commands/RunReadinessAssessment.cs).
- **Selected changes:** Add required AssessmentId. Persist complete fixed-as-of input snapshot and latest-adoption admission before child computation. At most one pending latest-adoption operation; source absence is recorded as a gap only after authoritative completeness, infrastructure failure remains transient.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs`.

## SetCriteriaTextOverlay

- **Handler → first owner:** `SetCriteriaTextOverlayHandler` → `CriteriaTextOverlay`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EditionId: Uuid`, `Identifier: string`, `ExpectedRevision: long`, `Content: CriteriaTextOverlayContent`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"CriteriaTextOverlay","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `CriteriaTextOverlayEntryRevised`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"CriteriaTextOverlayRegistration","firstHttpStatus":200,"terminalResultType":"CriteriaTextOverlayRegistration"}`.
- **Owner protocols:** Per-criterion overlay revision.
- **Preserved source validation:** [src/Compliance.Common/Features/Criteria/SetCriteriaTextOverlay.cs](../../../../src/Compliance.Common/Features/Criteria/Commands/SetCriteriaTextOverlay.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Criteria/CriteriaTextOverlayAuthorizationTests.cs`, `test/Compliance.Tests/Features/Criteria/SetCriteriaTextOverlayHandlerTests.cs`.

## SetFirmStaffStatus

- **Handler → first owner:** `SetFirmStaffStatusHandler` → `FirmStaffRecord`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/platform/firm-staff/{staff_member_id}/set-firm-staff-status/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `StaffMemberId: Uuid`, `IsActive: bool`, `Reason: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"FirmStaffRecord","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `FirmStaffChangeRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"FirmStaffMemberView","firstHttpStatus":200,"terminalResultType":"FirmStaffMemberView"}`.
- **Owner protocols:** Permanent staff identity mapping.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/SetFirmStaffStatus.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/SetFirmStaffStatus.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.

## StartControlEvaluation

- **Handler → first owner:** `StartControlEvaluationHandler` → `ControlEvaluation`.
- **Logical identity:** `{"field":"EvaluationId","type":"Uuid","required":true,"origin":"client","rule":"exact immutable creation retry"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}`; MCP enabled.
- **Exact fields:** `EvaluationId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `PlanVersionId: Uuid`, `RetestOfEvaluationId: Uuid?`.
- **Revision scope:** `{"field":null,"owner":"ControlEvaluation","counter":"No primary caller ResourceRevision. Enforce required authored source/epoch/version tokens, immutable creation identity or trusted proof/generation as declared; append uses physical OCC."}`.
- **Native facts:** `ControlEvaluationStarted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}`.
- **Owner protocols:** Independent evaluation and retest.
- **Preserved source validation:** [src/Compliance.Common/Features/Evaluations/StartControlEvaluation.cs](../../../../src/Compliance.Common/Features/Evaluations/Commands/StartControlEvaluation.cs).
- **Selected changes:** Add required EvaluationId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs`, `test/Compliance.Tests/Features/Evaluations/ControlEvaluationPlanHandlerTests.cs`, `test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs`, `test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## SubmitAuditResponse

- **Handler → first owner:** `SubmitAuditResponseHandler` → `AuditorResponse`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/submit-audit-response/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RequestId: Uuid`, `ResponseId: Uuid`, `Version: long`, `SubmissionNote: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditorResponse","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditorResponseSubmitted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Response/comment admission and request resolution.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditorRequests/SubmitAuditResponse.cs](../../../../src/Compliance.Common/Features/AuditorRequests/Commands/SubmitAuditResponse.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditorRequests/AuditorResponseCommandHandlerTests.cs`.

## SubmitControlEvaluation

- **Handler → first owner:** `SubmitControlEvaluationHandler` → `ControlEvaluation`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `ControlId: Uuid`, `EvaluationId: Uuid`, `ExpectedRevision: long`, `Conclusions: IReadOnlyList<EvaluationAssertionConclusion>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ControlEvaluation","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ControlEvaluationSubmitted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ControlEvaluationView","firstHttpStatus":200,"terminalResultType":"ControlEvaluationView"}`.
- **Owner protocols:** Independent evaluation and retest.
- **Preserved source validation:** [src/Compliance.Common/Features/Evaluations/SubmitControlEvaluation.cs](../../../../src/Compliance.Common/Features/Evaluations/Commands/SubmitControlEvaluation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs`, `test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs`, `test/Compliance.Tests/Features/Operations/OperationsFixture.cs`, `test/Compliance.Tests/Features/Work/ControlEvaluationWorkTests.cs`, `test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs`, `test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs`.

## SubmitRiskTreatmentActionCompletion

- **Handler → first owner:** `SubmitRiskTreatmentActionCompletionHandler` → `RiskTreatmentAction`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** personal_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `RiskId: Uuid`, `ActionId: Uuid`, `ExpectedRevision: long`, `Summary: string`, `EvidenceRequestIds: IReadOnlyList<Uuid>`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"RiskTreatmentAction","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `RiskTreatmentActionCompletionSubmitted`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"RiskTreatmentActionCompletionRegistration","firstHttpStatus":200,"terminalResultType":"RiskTreatmentActionCompletionRegistration"}`.
- **Owner protocols:** Risk ownership, treatment lines and actions.
- **Preserved source validation:** [src/Compliance.Common/Features/Risks/SubmitRiskTreatmentActionCompletion.cs](../../../../src/Compliance.Common/Features/Risks/Commands/SubmitRiskTreatmentActionCompletion.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs`.

## ValidateAuditPackage

- **Handler → first owner:** `ValidateAuditPackageHandler` → `AuditPackageManifest`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/validations/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `PackageId: Uuid`, `PackageVersion: long`, `ValidationKey: string`, `ExpectedRevision: long`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AuditPackageManifest","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AuditPackageValidated`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Manifest validation, immutable build and recipient delivery.
- **Preserved source validation:** [src/Compliance.Common/Features/AuditPackages/ValidateAuditPackage.cs](../../../../src/Compliance.Common/Features/AuditPackages/Commands/ValidateAuditPackage.cs).
- **Selected changes:** Add required CommandId. Add required owner-local ExpectedRevision; business Version remains an exact content version, not owner OCC.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AuditPackages/ValidateAuditPackageHandlerTests.cs`.

## VerifyAccessRemediation

- **Handler → first owner:** `VerifyAccessRemediationHandler` → `AccessReviewItem`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `CampaignId: Uuid`, `ItemId: Uuid`, `ExpectedRevision: long`, `PopulationId: Uuid`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"AccessReviewItem","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `AccessRemediationVerified`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"AccessRemediationVerificationView","firstHttpStatus":200,"terminalResultType":"AccessRemediationVerificationView"}`.
- **Owner protocols:** Campaign item and atomic bulk decision.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessReviews/VerifyAccessRemediation.cs](../../../../src/Compliance.Common/Features/AccessReviews/Commands/VerifyAccessRemediation.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs`, `test/Compliance.Tests/Features/Work/FitzAccessReviewCampaignWorkItemDirectoryTests.cs`.

## WithdrawCriterionNotApplicable

- **Handler → first owner:** `WithdrawCriterionNotApplicableHandler` → `CriterionApplicability`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http; `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals/{command_id}`; MCP disabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `ProgramId: Uuid`, `DecisionId: Uuid`, `ExpectedRevision: long`, `Rationale: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"CriterionApplicability","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `CriterionNotApplicableWithdrawn`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}`.
- **Owner protocols:** Per-criterion applicability line.
- **Preserved source validation:** [src/Compliance.Common/Features/ControlMappings/WithdrawCriterionNotApplicable.cs](../../../../src/Compliance.Common/Features/ControlMappings/Commands/WithdrawCriterionNotApplicable.cs).
- **Selected changes:** Add required CommandId.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs`.

## WithdrawServiceEngagementStaffProposal

- **Handler → first owner:** `WithdrawServiceEngagementStaffProposalHandler` → `ServiceEngagementIndependence`.
- **Logical identity:** `{"field":"CommandId","type":"Uuid","required":true,"origin":"client","rule":"one immutable authored operation"}`.
- **Entry / HTTP / MCP:** authorized_http_and_mcp; `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id}`; MCP enabled.
- **Exact fields:** `CommandId: Uuid`, `TenantId: Uuid`, `EngagementId: Uuid`, `StaffMemberId: Uuid`, `ExpectedRevision: long`, `Reason: string`.
- **Revision scope:** `{"field":"ExpectedRevision","owner":"ServiceEngagementIndependence","counter":"logical ResourceRevision of this owner","nullableInternalRule":null}`.
- **Native facts:** `ServiceEngagementMutationRecorded`.
- **Original response:** `{"mode":"immediate_original_result","responseType":"ServiceEngagementView","firstHttpStatus":200,"terminalResultType":"ServiceEngagementView"}`.
- **Owner protocols:** Service epoch and permanent practice commitments.
- **Preserved source validation:** [src/Compliance.Common/Features/AccessControl/WithdrawServiceEngagementStaffProposal.cs](../../../../src/Compliance.Common/Features/AccessControl/Commands/WithdrawServiceEngagementStaffProposal.cs).
- **Selected changes:** Add required CommandId. Rename ExpectedSequence to ExpectedRevision and compare the new target owner revision.
- **Required regressions:** `ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange`, `ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision`, `ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity`, `ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof`, `ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome`; include each applicable [ordered race](design-review-traces.json). Existing fixtures: `test/Compliance.Tests/Features/AccessControl/AttestServiceEngagementManagementWriteWallTests.cs`, `test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs`.
