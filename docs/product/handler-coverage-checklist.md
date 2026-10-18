# Handler coverage checklist

Derived from every command in [the feature-backed command plan](command-list.md): accepted P0/P1 requirements and M0 decisions. Query-only workflows and excluded hypotheses stay out. “Test reference” is a static symbol search; it does not claim behavior is validated. Existing handler behavior still needs focused TDD review.

Handler status `partial` means the command has a registered handler for only part of its accepted feature scope and remains unfinished.

## Current gap

- The plan contains 128 feature-backed commands, including 5 explicitly deferred by M0-D04/EN-05. Of the 123 active commands, 81 have complete handler coverage, 5 have partial handler coverage, and 37 still have no handler.
- Active command registration gaps: 37. Active commands without a handler test reference: 37. The five partial handlers are registered and tested but do not yet cover their accepted feature scope.
- Existing commands remain listed below to track established handler coverage and known transport or registration exceptions.
- Keep this layer open until every required command has a real handler, the owning host can dispatch it, and behavior tests pass. Only then start projectors.

| Requirement group | Command | Contract | Handler | Registration | Test reference |
|---|---|---:|---:|---:|---:|
| AccessControl | `AcknowledgeEngagementManagement` | present | present | present | referenced |
| AccessControl | `AmendServiceEngagement` | present | present | present | referenced |
| AccessControl | `ApproveSeparationOfDutiesWaiver` | present | present | present | referenced |
| AccessControl | `AssignRolePermission` | present | present | present | referenced |
| AccessControl | `AssignTeamMember` | present | present | present | referenced |
| AccessControl | `AssignTeamRole` | present | present | present | referenced |
| AccessControl | `CloseServiceEngagement` | present | present | present | referenced |
| AccessControl | `CreateServiceEngagement` | present | present | present | referenced |
| AccessControl | `DefineRole` | present | present | present | referenced |
| AccessControl | `DefineTeam` | present | present | present | referenced |
| AccessControl | `DeleteRole` | present | present | present | referenced |
| AccessControl | `DeleteTeam` | present | present | present | referenced |
| AccessControl | `DeprovisionMember` | present | present | present | referenced |
| AccessControl | `EvaluateClientIndependence` | present | present | present | referenced |
| AccessControl | `GrantAccess` | present | present | present | referenced |
| AccessControl | `ProposeServiceEngagementStaff` | present | present | present | referenced |
| AccessControl | `RecordDirectoryIndependenceReevaluation` | present (internal) | present | present | referenced |
| AccessControl | `RecordNonattestService` | present | present | present | referenced |
| AccessControl | `RecordSeparationOfDutiesWaiver` | present | present | present | referenced |
| AccessControl | `RegisterFirmStaff` | present | present | present | referenced |
| AccessControl | `RegisterMember` | present | present | present | referenced |
| AccessControl | `ReinstateMember` | present | present | present | referenced |
| AccessControl | `RemoveRolePermission` | present | present | present | referenced |
| AccessControl | `RemoveTeamMember` | present | present | present | referenced |
| AccessControl | `RemoveTeamRole` | present | present | present | referenced |
| AccessControl | `RenameRole` | present | present | present | referenced |
| AccessControl | `ReviseIndependenceRules` | present | present | present | referenced |
| AccessControl | `RevokeAccessGrant` | present | present | present | referenced |
| AccessControl | `SetFirmStaffStatus` | present | present | present | referenced |
| AccessControl | `SuspendMember` | present | present | present | referenced |
| AccessControl | `WithdrawServiceEngagementStaffProposal` | present | present | present | referenced |
| AccessReviews | `AcceptAccessPopulation` | present | present | present | referenced |
| AccessReviews | `ApproveAccessExpectation` | present | present | present | referenced |
| AccessReviews | `ClassifyAccessPrincipal` | present | present | present | referenced |
| AccessReviews | `CompleteAccessReviewCampaign` | present | present | present | referenced |
| AccessReviews | `ExemptAccessExpectation` | present | present | present | referenced |
| AccessReviews | `ExemptAccessRemediation` | present | present | present | referenced |
| AccessReviews | `ExemptMissingAccessPopulation` | present | present | present | referenced |
| AccessReviews | `LaunchAccessReviewCampaign` | present | present | present | referenced |
| AccessReviews | `OpenAccessPopulation` | present | present | present | referenced |
| AccessReviews | `ProposeAccessExpectation` | present | present | present | referenced |
| AccessReviews | `RecordAccessDecision` | present | present | present | referenced |
| AccessReviews | `RecordAccessPopulationFacts` | present | present | present | referenced |
| AccessReviews | `RecordAccessRemediationChange` | present | present | present | referenced |
| AccessReviews | `RecordBulkAccessDecision` | present | present | present | referenced |
| AccessReviews | `VerifyAccessRemediation` | present | present | present | referenced |
| Applications | `AcceptApplicationImport` | present | present | present | referenced |
| Applications | `AddApplicationRelationship` | present | present | present | referenced |
| Applications | `ApplyApplicationImportEffect` | present | present | present | referenced |
| Applications | `CancelApplicationImport` | present | present | present | referenced |
| Applications | `CorrelateApplicationImportRow` | present | present | present | referenced |
| Applications | `DecideAccessReviewScope` | present | present | present | referenced |
| Applications | `DeclareApplication` | present | present | present | referenced |
| Applications | `DeclareSystemInstance` | present | present | present | referenced |
| Applications | `ExecuteApplicationImport` | present | present | present | referenced |
| Applications | `FreezeApplicationImportOmissionProposal` | present | present | present | referenced |
| Applications | `RemoveApplicationRelationship` | present | present | present | referenced |
| Applications | `RetireApplication` | present | present | present | referenced |
| Applications | `RetireSystemInstance` | present | present | present | referenced |
| Applications | `ReviseApplication` | present | present | present | referenced |
| Applications | `ReviseApplicationMcpInput` | present | present | present | referenced |
| Applications | `StageApplicationImport` | present | present | present | referenced |
| Boundaries | `ApproveBoundary` | present | present | present | referenced |
| Boundaries | `CreateBoundary` | present | present | present | referenced |
| Boundaries | `DiscardBoundaryDraft` | present | present | present | no reference |
| Boundaries | `ProposeBoundarySuccessor` | present | present | present | no reference |
| Boundaries | `ReviewBoundary` | present | present | present | referenced |
| Boundaries | `ReviseBoundaryDraft` | present | present | present | no reference |
| Commitments | `ApproveCommitmentDraft` | present | present | present | referenced |
| Commitments | `CreateCommitmentDraft` | present | present | present | referenced |
| Commitments | `ReviewCommitmentDraft` | present | present | present | referenced |
| Commitments | `ReviseCommitmentDraft` | present | present | present | no reference |
| ControlMappings | `ProposeControlCriterionMapping` | present | present | present | referenced |
| ControlMappings | `ProposeCriterionNotApplicable` | present | present | present | referenced |
| ControlMappings | `RetireControlCriterionMapping` | present | present | present | referenced |
| ControlMappings | `ReviewControlCriterionMapping` | present | present | present | referenced |
| ControlMappings | `ReviewCriterionApplicability` | present | present | present | referenced |
| ControlMappings | `WithdrawCriterionNotApplicable` | present | present | present | referenced |
| Controls | `ApproveControl` | present | present | present | referenced |
| Controls | `CreateControlDraft` | present | present | present | referenced |
| Controls | `DesignateControlOwnerPerson` | present | present | present | referenced |
| Controls | `DiscardControlDraft` | present | present | present | referenced |
| Controls | `ProposeControlRetirement` | present | present | present | referenced |
| Controls | `ProposeControlSuccessor` | present | present | present | referenced |
| Controls | `RetireControl` | present | present | present | referenced |
| Controls | `ReviewControl` | present | present | present | referenced |
| Controls | `ReviseControlDraft` | present | present | present | no reference |
| Controls | `WithdrawControlProposal` | present | present | present | referenced |
| Criteria | `SetCriteriaTextOverlay` | present | present | present | referenced |
| Evaluations | `DefineControlEvaluationPlan` | present | present | present | referenced |
| Evaluations | `DisposeControlEvaluationDeviation` | present | present | present | referenced |
| Evaluations | `RaiseEvaluationDeviationFinding` | present | present | present | referenced |
| Evaluations | `RecordControlEvaluationStep` | present | present | present | referenced |
| Evaluations | `ReviewControlEvaluation` | present | present | present | referenced |
| Evaluations | `StartControlEvaluation` | present | present | present | referenced |
| Evaluations | `SubmitControlEvaluation` | present | present | present | referenced |
| Evidence | `ApproveEvidenceRedaction` | present | present | present | referenced |
| Evidence | `CancelEvidenceRequest` | present | present | present | referenced |
| Evidence | `FulfilEvidenceRequest` | present | present | present | referenced |
| Evidence | `OpenEvidenceRequest` | present | present | present | referenced |
| Evidence | `PrepareEvidenceRedaction` | present | present | present | referenced |
| Operations | `ApproveControlOperatingPlan` | present | present | present | referenced |
| Operations | `AttestControlOccurrence` | present | present | present | referenced |
| Operations | `CorrectControlAttestation` | present | present | present | referenced |
| Operations | `OpenControlOccurrence` | present | present | present | referenced |
| Operations | `ProposeControlOperatingPlan` | present | present | present | referenced |
| Operations | `ReviewControlOccurrence` | present | present | present | referenced |
| Policies | `ApprovePolicy` | present | present | present | referenced |
| Policies | `ApprovePolicyRetirement` | present | present | present | referenced |
| Policies | `ConfirmPolicyReview` | present | present | present | referenced |
| Policies | `CreatePolicyDraft` | present | present | present | referenced |
| Policies | `DiscardPolicyDraft` | present | present | present | no reference |
| Policies | `ProposePolicyRetirement` | present | present | present | no reference |
| Policies | `ProposePolicySuccessor` | present | present | present | referenced |
| Policies | `ReviewPolicyDraft` | present | present | present | referenced |
| Policies | `RevisePolicyDraft` | present | present | present | no reference |
| PolicyDistribution | `AcknowledgePolicy` | present | present | present | referenced |
| PolicyDistribution | `ApproveCampaignWaiver` | present | present | present | referenced |
| PolicyDistribution | `CloseCampaign` | present | present | present | no reference |
| PolicyDistribution | `DefineTrainingRequirement` | present | present | present | referenced |
| PolicyDistribution | `LaunchPolicyCampaign` | present | present | present | referenced |
| PolicyDistribution | `LaunchTrainingCampaign` | present | present | present | no reference |
| PolicyDistribution | `ReconcileCampaignAudience` | present | present | present | no reference |
| PolicyDistribution | `RecordTrainingCompletion` | present | present | present | referenced |
| PolicyDistribution | `ReviseTrainingRequirement` | present | present | present | no reference |
| Programs | `CreateClientService` | present | present | present | referenced |
| Programs | `CreateProgram` | present | present | present | referenced |
| Programs | `RetireClientService` | present | present | present | no reference |
| Programs | `ReviseClientService` | present | present | present | no reference |
| Programs | `ReviseProgram` | present | present | present | referenced |
| Programs | `SelectProgramCriteriaEdition` | present | present | present | referenced |
| Providers | `CloseProviderCoverageGap` | present | present | present | no reference |
| Providers | `LinkProviderCoverageGapRiskAcceptance` | present | present | present | no reference |
| Providers | `RecordAssuranceReport` | present | present | present | referenced |
| Providers | `RecordProvider` | present | present | present | referenced |
| Providers | `RecordProviderCoverageGap` | present | present | present | no reference |
| Providers | `RecordProviderReview` | present | present | present | referenced |
| Providers | `ReviseAssuranceReport` | present | present | present | referenced |
| Providers | `ReviseProvider` | present | present | present | referenced |
| Readiness | `AnnotateReadinessGap` | present | present | present | referenced |
| Readiness | `DecideReadiness` | present | present | present | referenced |
| Readiness | `DecideTypeIEntry` | present | present | present | referenced |
| Readiness | `PlanReadinessGap` | present | present | present | referenced |
| Readiness | `RunReadinessAssessment` | present | present | present | referenced |
| Remediation | `AddCorrectiveAction` | present | present | present | referenced |
| Remediation | `CloseFinding` | present | present | present | referenced |
| Remediation | `CompleteCorrectiveAction` | present | present | present | referenced |
| Remediation | `LinkFindingAcceptance` | present | present | present | referenced |
| Remediation | `RaiseFinding` | present | present | present | referenced |
| Remediation | `RaiseOccurrenceFinding` | present | present | present | referenced |
| Remediation | `ReopenFinding` | present | present | present | referenced |
| Remediation | `ReviseFinding` | present | present | present | referenced |
| Responsibilities | `AssignResponsibility` | present | present | present | referenced |
| Responsibilities | `RevokeResponsibility` | present | present | present | referenced |
| Retention | `PlaceArtifactLegalHold` | present | present | present | referenced |
| Retention | `RecordArtifactRetentionBasis` | present | present | present | referenced |
| Retention | `ReleaseArtifactLegalHold` | present | present | present | referenced |
| Risks | `AcceptRisk` | present | present | present | referenced |
| Risks | `AddRiskTreatmentAction` | present | present | present | referenced |
| Risks | `AssignRiskOwner` | present | present | present | referenced |
| Risks | `CancelRiskTreatmentAction` | present | present | present | no reference |
| Risks | `ChooseRiskTreatment` | present | present | present | no reference |
| Risks | `CreateRiskDraft` | present | present | present | no reference |
| Risks | `ProposeRiskControlTreatment` | present | present | present | referenced |
| Risks | `PublishRiskMethodVersion` | present | present | present | no reference |
| Risks | `RaiseRiskReassessmentTriggers` | present | present | present | referenced |
| Risks | `RecordRiskAssessment` | present | present | present | referenced |
| Risks | `RetireRiskControlTreatment` | present | present | present | no reference |
| Risks | `ReviewRiskControlTreatment` | present | present | present | referenced |
| Risks | `ReviewRiskTreatmentActionCompletion` | present | present | present | referenced |
| Risks | `ReviseRiskDraft` | present | present | present | no reference |
| Risks | `ReviseRiskTreatmentAction` | present | present | present | referenced |
| Risks | `SubmitRiskTreatmentActionCompletion` | present | present | present | referenced |
| Snapshots | `AmendProgramScopeSnapshot` | present | present | present | referenced |
| Snapshots | `FreezeProgramScopeSnapshot` | present | present | present | referenced |
| TechnologyInventory | `RecordDataFlow` | present | present | present | no reference |
| TechnologyInventory | `RecordInformationAsset` | present | present | present | referenced |
| TechnologyInventory | `RecordLocation` | present | present | present | no reference |
| TechnologyInventory | `RecordOperationalProcess` | present | present | present | no reference |
| TechnologyInventory | `RecordTechnologyComponent` | present | present | present | referenced |
| TechnologyInventory | `ReviseDataFlow` | present | present | present | no reference |
| TechnologyInventory | `ReviseInformationAsset` | present | present | present | no reference |
| TechnologyInventory | `ReviseLocation` | present | present | present | no reference |
| TechnologyInventory | `ReviseOperationalProcess` | present | present | present | no reference |
| TechnologyInventory | `ReviseTechnologyComponent` | present | present | present | referenced |
| Tenants | `AcceptTenantInvitation` | present | present | present | referenced |
| Tenants | `ActivateTenant` | present | present | present | referenced |
| Tenants | `ChangeTenantSlug` | present | present | present | referenced |
| Tenants | `ConfirmTenantSlug` | present | present | present | referenced |
| Tenants | `ConfirmTenantSlugSurrender` | present | present | present | referenced |
| Tenants | `GrantPlatformOperator` | present | present | present | no reference |
| Tenants | `InviteOrganizationMember` | present | present | present | referenced |
| Tenants | `InviteTenantMember` | present | present | present | no reference |
| Tenants | `OffboardTenant` | present | present | present | referenced |
| Tenants | `ReactivateTenant` | present | present | present | no reference |
| Tenants | `RegisterTenant` | present | present | present | referenced |
| Tenants | `RegisterTenantOwner` | present | present | present | referenced |
| Tenants | `RegisterTenantSlug` | present | present | present | referenced |
| Tenants | `RejectTenantSlug` | present | present | present | referenced |
| Tenants | `RejectTenantSlugSurrender` | present | present | present | referenced |
| Tenants | `RequestTenantSlugSurrender` | present | present | present | no reference |
| Tenants | `RevokePlatformOperator` | present | present | present | no reference |
| Tenants | `SeedPlatformOperatorRoster` | present | present | present | no reference |
| Tenants | `SurrenderTenantSlug` | present | present | present | referenced |
| Tenants | `SuspendTenant` | present | present | present | no reference |
| UserIdentities | `CompleteEmailChallenge` | present | present | present | referenced |
| UserIdentities | `CompleteIdentityRecovery` | present | present | present | referenced |
| UserIdentities | `ContinueWithDeveloperIdentity` | present | present | present | referenced |
| UserIdentities | `ContinueWithOidcProvider` | present | present | present | referenced |
| UserIdentities | `IssueEmailChallenge` | present | present | present | no reference |
| UserIdentities | `LinkOidcProviderIdentity` | present | present | present | referenced |
| UserIdentities | `ReserveEmail` | present | present | present | referenced |
| UserIdentities | `StartIdentityRecovery` | present | present | present | referenced |
| Work | `AssignWorkItem` | present | present | present | referenced |
| Work | `ClaimWorkItem` | present | present | present | referenced |
| Work | `DelegateWorkItem` | present | present | present | referenced |
| Work | `EscalateWorkItem` | present | present | present | referenced |
| Work | `SetWorkDigestPreference` | present | present | present | referenced |
| Workforce | `AmendWorkforceRosterSnapshot` | present | present | present | referenced |
| Workforce | `CorrelatePersonMembership` | present | present | present | referenced |
| Workforce | `FreezeWorkforceRosterSnapshot` | present | present | present | referenced |
| Workforce | `ReconcileWorkforceSourceObservation` | present | present | present | referenced |
| Workforce | `RecordPerson` | present | present | present | referenced |
| Workforce | `RecordServiceIdentity` | present | present | present | referenced |
| Workforce | `RecordWorkRelationship` | present | present | present | referenced |
| Workforce | `RecordWorkforceSourceObservation` | present | present | present | referenced |
| Workforce | `ResolveWorkforceObservation` | present | present | present | referenced |
| Workforce | `RevisePerson` | present | present | present | referenced |
| Workforce | `ReviseServiceIdentity` | present | present | present | referenced |
| Workforce | `ReviseWorkRelationship` | present | present | present | referenced |
| Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations) | `CaptureEvidenceArtifact` | present | present | present | referenced |
| Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations) | `RecordEvidenceArtifactInspection` | present | present | present | referenced |
| Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations) | `IssueArtifactDownload` | present | present | present | referenced |
| Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations) | `LinkEvidenceSupport` | present | present | present | referenced |
| Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations) | `ReleaseEvidenceArtifactQuarantine` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `AssignArtifactHandlingPolicy` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `PlaceArtifactEngagementHold` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `RecordEngagementReportIssued` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `ReleaseArtifactEngagementHold` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `ApproveArtifactDisclosure` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `RecordArtifactDisclosureDelivery` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `RevokeArtifactDisclosure` | present | present | present | referenced |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `ApproveArtifactDisposition` | present | missing | check | no reference |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `ExecuteArtifactDisposition` | present | missing | check | no reference |
| Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md) | `RecordArtifactDispositionResult` | present | present | present | `RecordArtifactDispositionResultHandlerTests` |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `CreateAuditPackage` | present | partial | present | `CreateAuditPackageHandlerTests` |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `CreateAdvisorHandoff` | present | missing | check | no reference |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `ReviseAdvisorHandoffSelection` | present | missing | check | no reference |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `MarkAdvisorHandoffReadyForReview` | present | missing | check | no reference |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `CloseAdvisorHandoff` | present | present | present | referenced |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `RevokeAdvisorHandoff` | present | present | present | referenced |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `RecordAdvisorFeedback` | present | missing | check | no reference |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `CorrectAdvisorFeedback` | present | missing | check | no reference |
| Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md) | `RespondToAdvisorFeedback` | present | missing | check | no reference |
| Control operation and recurrence — [R2-01](backlog.md#r2-01-assign-control-ownership-and-operating-cadence), [T2-02](backlog.md#t2-02-operate-a-recurring-control-calendar), R2-04, T2-03; [M0-D19](decisions/m0-d19-periodic-reviews.md) | `GenerateDueControlOccurrences` | present | present | present | referenced |
| Inventory and commitment lifecycle — [R1-10](backlog.md#r1-10-establish-the-application-inventory-and-review-scope), [R1-13](backlog.md#r1-13-record-service-commitments-system-requirements-and-user-responsibilities) | `ReviseSystemInstance` | present | present | present | referenced |
| Inventory and commitment lifecycle — [R1-10](backlog.md#r1-10-establish-the-application-inventory-and-review-scope), [R1-13](backlog.md#r1-13-record-service-commitments-system-requirements-and-user-responsibilities) | `RetireCommitment` | present | present | present | referenced |
| Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met) | `StageReadinessMaterialImport` | present | deferred | deferred | deferred |
| Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met) | `ResolveReadinessMaterialImportRow` | present | deferred | deferred | deferred |
| Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met) | `AcceptReadinessMaterialImport` | present | deferred | deferred | deferred |
| Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met) | `CancelReadinessMaterialImport` | present | deferred | deferred | deferred |
| Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met) | `ExecuteReadinessMaterialImport` | present | deferred | deferred | deferred |
| Work queue and digest delivery — [R2-11](backlog.md#r2-11-manage-accountable-compliance-work); [M0-D15](decisions/m0-d15-work-queue.md) | `ScheduleWeeklyWorkDigest` | present | missing | check | no reference |
| Work queue and digest delivery — [R2-11](backlog.md#r2-11-manage-accountable-compliance-work); [M0-D15](decisions/m0-d15-work-queue.md) | `DeliverWeeklyWorkDigest` | present | missing | check | no reference |
| Work queue and digest delivery — [R2-11](backlog.md#r2-11-manage-accountable-compliance-work); [M0-D15](decisions/m0-d15-work-queue.md) | `RecordWorkDigestDelivery` | present | missing | check | no reference |
| Type I engagement and baseline — [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot) | `CreateExaminationEngagement` | present | present | present | referenced |
| Type I engagement and baseline — [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot) | `ReviseExaminationEngagement` | present | present | present | referenced |
| Type I engagement and baseline — [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot) | `FreezeTypeIBaseline` | present | missing | check | no reference |
| Type I engagement and baseline — [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot) | `AmendTypeIBaseline` | present | missing | check | no reference |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `CreateSystemDescriptionDraft` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `ReviseSystemDescriptionDraft` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `BindSystemDescriptionSources` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `ReviewSystemDescription` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `ApproveSystemDescription` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `ProposeSystemDescriptionSuccessor` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `RecordSystemDescriptionNoChangeDecision` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `BindSystemDescriptionToExamination` | present | present | present | referenced |
| Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md) | `GenerateSystemDescriptionExport` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordAuditorRequest` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `ReviseAuditorRequest` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `AssignAuditorRequest` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordAuditorRequestComment` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `CreateAuditResponseDraft` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `ReviseAuditResponseDraft` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `SubmitAuditResponse` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `ReviewAuditResponse` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `DeliverAuditResponse` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordAuditorResponseDisposition` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `ResolveAuditorRequest` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `ReopenAuditorRequest` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordSampleSelection` | present | present | present | referenced |
| Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md) | `AmendSampleSelection` | present | present | present | referenced |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `RequestAuditPackageBuild` | present | present | present | `AuditPackageCommandHandlerTests` |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `RecordAuditPackageBuildResult` | present | present | present | `RecordAuditPackageBuildResultHandlerTests` |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ValidateAuditPackage` | present | partial | present | `ValidateAuditPackageHandlerTests` |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ApproveAuditPackageValidationOverride` | present | present | present | referenced |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ApproveAuditPackageDisclosure` | present | present | present | referenced |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `RecordAuditPackageDelivery` | present | present | present | referenced |
| Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `AmendAuditPackage` | present | partial | present | `AmendAuditPackageHandlerTests` |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordManagementAssertion` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `ApproveManagementAssertion` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordManagementRepresentationLetter` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `SignManagementRepresentationLetter` | present | present | present | referenced |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordExaminationOutcome` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordManagementExaminationReview` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `ProposeTypeIIOperatingPlan` | present | missing | check | no reference |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `ReviewTypeIIOperatingPlan` | present | present | present | referenced |
| Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md) | `ApproveTypeIIOperatingPlan` | present | missing | check | no reference |
| Observation periods and continuous operation — [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period) | `StartTypeIIObservationPeriod` | present | missing | check | no reference |
| Observation periods and continuous operation — [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period) | `AmendTypeIIObservationPeriod` | present | present | present | referenced |
| Observation periods and continuous operation — [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period) | `ProposeProgramRollover` | present | missing | check | no reference |
| Observation periods and continuous operation — [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period) | `ApproveProgramRollover` | present | missing | check | no reference |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `RecordSignificantChange` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `ReviseSignificantChange` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `AssessSignificantChange` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `RecordComplianceIncident` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `ReviseComplianceIncident` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `AssessComplianceIncident` | present | present | present | referenced |
| Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md) | `RecordComplianceIncidentResolution` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ApproveTypeIIPeriodClose` | present | missing | check | no reference |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `FreezeTypeIIPeriodClose` | present | missing | check | no reference |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `AmendTypeIIPeriodClose` | present | missing | check | no reference |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `DefineAuditPopulation` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ReviseAuditPopulationDefinition` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `RecordAuditPopulationFacts` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ClassifyAuditPopulationRow` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `ReconcileAuditPopulation` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `GenerateAuditPopulation` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `FreezeAuditPopulation` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `AmendAuditPopulation` | present | present | present | referenced |
| Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md) | `GenerateAuditPopulationExport` | present | present | present | `GenerateAuditPopulationExportHandlerTests` |
| Professional authority and actual assignments — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md) | `AcceptServiceEngagement` | present | missing | check | no reference |
| Professional authority and actual assignments — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md) | `AssignServiceEngagementStaff` | present | missing | check | no reference |
| Professional authority and actual assignments — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md) | `RevokeServiceEngagementStaff` | present | missing | check | no reference |
| Independence authority — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md) | `RatifyIndependenceRuleVersion` | present | missing | check | no reference |
| Independence authority — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md) | `RecordPartnerIndependenceEvaluation` | present | missing | check | no reference |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RequestClientExport` | present | missing | check | no reference |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordClientExportBuildResult` | present | present | present | `RecordClientExportBuildResultHandlerTests` |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordClientExportDelivery` | present | present | present | `RecordClientExportDeliveryHandlerTests` |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RequestOwnAttestRecordPackage` | present | missing | check | no reference |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordOwnAttestRecordPackageResult` | present | missing | check | no reference |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `RecordOwnAttestRecordDelivery` | present | missing | check | no reference |
| Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md) | `CompleteTenantOffboarding` | present | missing | check | no reference |
| Client-specific federation — [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations), [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md) | `ConfigureTenantIdentityProvider` | present | partial | present | `TenantIdentityProviderCommandHandlerTests` |
| Client-specific federation — [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations), [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md) | `TestTenantIdentityProvider` | present | missing | check | no reference |
| Client-specific federation — [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations), [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md) | `DisableTenantIdentityProvider` | present | present | present | referenced |
| Client-specific federation — [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations), [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md) | `RotateTenantIdentityProviderCredentials` | present | partial | present | `TenantIdentityProviderCommandHandlerTests` |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `CreateMethodologyTemplate` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ReviseMethodologyTemplateDraft` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ReviewMethodologyTemplate` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ApproveMethodologyTemplateVersion` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `RetireMethodologyTemplate` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ApplyMethodologyTemplate` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ProposeTemplateSuccessorAdoption` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `RecordTemplateSuccessorDisposition` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ProposeDeidentifiedTemplateContribution` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `ReviewTemplateContribution` | present | present | present | referenced |
| Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients) | `AcceptTemplateContribution` | present | present | present | referenced |

## Deferred handler work

The five readiness-material import contracts remain part of the future command universe, but are excluded from the active handler count by [M0-D04](decisions/m0-d04-readiness-material.md) and the EN-05 prerequisites. R1-09 is P1 and has no scheduled import work until representative source material, canonical data shapes, and storage internals are settled. Implement the handlers only after preview, durable reconciliation, an immutable accepted plan, and safe idempotent target execution exist.

## Remaining source, authority, and adapter prerequisites

These dependencies are found in the existing contracts, aggregates, and accepted decisions. They are not reasons to add handlers that guess at source facts or authority. Close each prerequisite, then add a red behavior test and implement the affected handler.

| Commands | Prerequisite before a correct handler can be wired |
|---|---|
| `ApproveArtifactDisposition`, `ExecuteArtifactDisposition` | Build a tenant-scoped eligibility reader that binds the exact artifact, retention basis, handling policy, active legal and engagement holds, historical reliance, and storage readiness. Execution also needs a durable deletion provider. `ArtifactDisposition` already rejects missing or changed eligibility snapshots. |
| `RecordArtifactDispositionResult` reactor integration | The handler now accepts results only from the named internal worker, bound to the exact execution-request event and attempt number. The storage deletion reactor and its durable provider outcome remain to be implemented in the reactor layer. |
| `CreateAdvisorHandoff`, `ReviseAdvisorHandoffSelection`, `MarkAdvisorHandoffReadyForReview` | Add a resolver for every supported record kind that proves tenant, program, current exact version, and shareable status. The aggregate accepts IDs but cannot establish those source facts. Readiness must be restricted to the Compliance Lead as required by [M0-D14](decisions/m0-d14-advisor-collaboration.md). |
| `RecordAdvisorFeedback`, `CorrectAdvisorFeedback`, `RespondToAdvisorFeedback` | Add the M0-D14 scoped advisor/guest authorization model, including accepted firm-staff assignments, program-scoped expiring guest grants, and attribution rules. The current commands use ordinary client-management authorization, which cannot authorize the specified advisor actors. |
| `ScheduleWeeklyWorkDigest`, `DeliverWeeklyWorkDigest`, `RecordWorkDigestDelivery` | Add a bounded weekly recipient/snapshot source, stable digest attempt identity, and a trusted email delivery adapter with durable outcome callbacks. The existing digest query and preference do not provide delivery state. |
| `FreezeTypeIBaseline`, `AmendTypeIBaseline` | Add the authoritative baseline preview/source builder and a complete amendment snapshot. A handler cannot freeze or amend a baseline from a date and an opaque revision alone. |
| System-description Type II successor scope — T1-02, T2-07, T3-01 | Draft creation and revision accept structured sections; source refresh verifies the complete Type I baseline or exact approved Type II plan and frozen close manifest. The aggregate successor path still preserves its existing scope and cannot create a Type II period version; add a period-scoped successor transition before this part of the feature is complete. |
| `CreateAuditPackage`, `ValidateAuditPackage`, `AmendAuditPackage` | Type I baseline and latest Type II close-snapshot resolution now verify exact source type, record ID, version ID, revision, and content hash. Type I/II amendments support removing records from the exact prior selection; adding sources or changing the frozen snapshot requires a richer command contract. Validation verifies the stored archive digest and exact ZIP paths, lengths, and member hashes against the manifest, and records non-overridable findings for source or archive mismatches. Canonical source metadata remains open. The build request and exact worker-result handlers persist a manifest-bound attempt and callback; deterministic package-content assembly and the build worker reactor remain required for the full feature. |
| `RecordManagementAssertion`, `ApproveManagementAssertion`, `RecordManagementRepresentationLetter`, `RecordExaminationOutcome`, `RecordManagementExaminationReview`, `ProposeTypeIIOperatingPlan`, `ApproveTypeIIOperatingPlan` | Complete the exact package, description, examination, signer, and outcome source contracts. Add trusted CEO/CISO role evidence for approval; neither plain claims nor client-authored signer lists prove executive authority. |
| `StartTypeIIObservationPeriod`, `ProposeProgramRollover`, `ApproveProgramRollover` | Add the approved Type I baseline and complete opening source/gap snapshot to period start. Rollover must bind the exact closed period and auditor outcome; approval requires trusted role evidence. |
| `ApproveTypeIIPeriodClose`, `FreezeTypeIIPeriodClose`, `AmendTypeIIPeriodClose` | Add trusted CEO/CISO role resolution and the close snapshot builder that verifies every source reference, expected occurrence, and acknowledgement. The aggregate requires those exact facts for approval and freeze. |
| `AcceptServiceEngagement`, `AssignServiceEngagementStaff`, `RevokeServiceEngagementStaff` | Add verified partner authority, current firm-staff directory snapshots, ratified rule versions, exact boundary approvals, and management acknowledgements. Assignment and revocation also need the authoritative effective-date staff source. |
| `RatifyIndependenceRuleVersion`, `RecordPartnerIndependenceEvaluation` | Resolve the still-open professional ratification policy in [M0-D26](decisions/m0-d26-independence.md) / issue #343 and model trusted quality-partner authority. An `IPlatformOperatorRequest` does not establish firm quality-partner authority. Evaluation also needs verified partner identity and the exact current source set. |
| `RequestClientExport`, `RequestOwnAttestRecordPackage`, `RecordOwnAttestRecordPackageResult`, `RecordOwnAttestRecordDelivery`, `CompleteTenantOffboarding` | Add source-complete export/package builders, durable encrypted artifact storage and delivery receipts, plus a bounded offboarding coordinator that verifies all export and retention obligations before completion. `RecordClientExportBuildResult` now accepts only the named worker, binds the callback to the persisted initial request or exact prior failure event, verifies the frozen source snapshot and deterministic attempt identity, and records the worker as a system process. `RecordClientExportDelivery` records a revision-fenced, member-attributed receipt after a successful build and any required attest-package deliveries; it does not verify the storage provider's encryption or delivery itself. |
| `ConfigureTenantIdentityProvider`, `TestTenantIdentityProvider`, `RotateTenantIdentityProviderCredentials` | Configure and rotation now persist the supplied opaque credential reference and require a follow-up test before the connection becomes enabled. The test handler, a secure vault resolver that verifies references, and a provider test/rotation adapter with verifiable outcomes remain. `DisableTenantIdentityProvider` records an administrator-attributed transition; sign-in routing must consume that state before F1-06 is complete. |

## Order of work

1. Backfill handler behavior for existing accepted command gaps, in feature order.
2. Implement handlers for the feature-backed additions, with focused red tests before implementation and green tests afterward.
3. Register each handler in the shared host and verify request authorization and tenant scope.
4. Run focused tests and repository unit tests; record any requirement that cannot be implemented because its owning aggregate or infrastructure is not modeled.
5. Do not begin projectors until the handler checklist is complete.
