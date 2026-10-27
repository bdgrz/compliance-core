# Aggregate implementation status and priority

Generated from the definitive aggregate catalog (129 business/authority aggregates), the current source tree, and the product backlog on 2026-10-27.

See [dependency-ordered aggregate backlog](aggregate-backlog.md) for the selected choreography order and live issue dependencies, and the [Location review](location-review-2026-10-27.md) for its focused replay evidence. The catalog is the authority for the intended aggregate set. It cautions that source presence does not establish target conformance. “Source present” therefore means a source file exists; it does not claim the aggregate satisfies every selected invariant or has passed full qualification. ComplianceProgram, SystemBoundary, ClientService, Member, Role, Team and TeamMember are marked done for aggregate scope based on their focused tests and Jev review; repository-wide qualification remains separate.

Priority is mapped only when an aggregate is named in a backlog story's body; ComplianceProgram is explicitly mapped to R1-01 because that story owns the program plan and continuing identity. “No direct story mapping” means the backlog does not name the aggregate in a story body; it does not mean the aggregate has no product value.

| Aggregate | Current implementation status | Backlog priority | Current source |
| --- | --- | --- | --- |
| `AccessExpectation` | Source present; target conformance not verified | P0 (R2-06)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessExpectation.cs` |
| `AccessGrant` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessControl/Aggregates/AccessGrant.cs` |
| `AccessPopulation` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulation.cs` |
| `AccessPopulationException` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulationException.cs` |
| `AccessPrincipalClassification` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPrincipalClassification.cs` |
| `AccessReviewCampaign` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewCampaign.cs` |
| `AccessReviewDecisionBatch` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewDecisionBatch.cs` |
| `AccessReviewItem` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewItem.cs` |
| `AdvisorFeedbackThread` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AdvisorCollaboration/Aggregates/AdvisorFeedbackThread.cs` |
| `AdvisorHandoff` | Source present; target conformance not verified | P0 (R2-08) | `src/Compliance.Core/Features/AdvisorCollaboration/Aggregates/AdvisorHandoff.cs` |
| `ApplicationImportBatch` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/ApplicationImportBatch.cs` |
| `ApplicationImportSource` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/ApplicationImportSource.cs` |
| `ApplicationRelationship` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/ApplicationRelationship.cs` |
| `ArtifactDisclosure` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisclosure.cs` |
| `ArtifactDisposition` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisposition.cs` |
| `ArtifactDownloadGrant` | Core aggregate and terminal event contracts implemented; request/process/read flow and full qualification pending | No direct story mapping | `src/Compliance.Core/Features/Evidence/Aggregates/ArtifactDownloadGrant.cs` |
| `ArtifactEngagementHold` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactEngagementHold.cs` |
| `ArtifactLegalHold` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactLegalHold.cs` |
| `ArtifactRetentionBasis` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactRetentionBasis.cs` |
| `AuditObservation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/AuditObservation.cs` |
| `AuditOutcome` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/AuditOutcome.cs` |
| `AuditPackageDelivery` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditPackages/Aggregates/AuditPackageDelivery.cs` |
| `AuditPackageManifest` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditPackages/Aggregates/AuditPackageManifest.cs` |
| `AuditPopulation` | Source present; target conformance not verified | P1 (T3-02) | `src/Compliance.Core/Features/AuditPopulations/Aggregates/AuditPopulation.cs` |
| `AuditorFeedbackThread` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorFeedbackThread.cs` |
| `AuditorRequest` | Source present; target conformance not verified | P1 (T1-04)<br>P1 (T3-03) | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs` |
| `AuditorResponse` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorResponse.cs` |
| `AuditorSampleSelection` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorSampleSelection.cs` |
| `ClientExport` | Source present; target conformance not verified | P1 (F1-01) | `src/Compliance.Core/Features/Exports/Aggregates/ClientExport.cs` |
| `ClientIndependenceDecision` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/ClientIndependenceDecision.cs` |
| `ClientService` | Done (aggregate scope): Jev reviewed; 35 focused tests pass | P0 (R1-02) | `src/Compliance.Core/Features/Programs/Aggregates/ClientService.cs` |
| `ClientServiceIndependence` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/ClientServiceIndependence.cs` |
| `CommitmentDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Commitments/Aggregates/CommitmentDraft.cs` |
| `ComplianceIncident` | Source present; target conformance not verified | P1 (T2-07) | `src/Compliance.Core/Features/Incidents/Aggregates/ComplianceIncident.cs` |
| `ComplianceProgram` | Done: Jev reviewed; 116 focused tests pass | P0 (R1-01) | `src/Compliance.Core/Features/Programs/Aggregates/ComplianceProgram.cs` |
| `ControlCriterionMapping` | Source present; target conformance not verified | P0 (R1-06) | `src/Compliance.Core/Features/ControlMappings/Aggregates/ControlCriterionMapping.cs` |
| `ControlDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Controls/Aggregates/ControlDraft.cs` |
| `ControlEvaluation` | Source present; target conformance not verified | P0 (R2-05) | `src/Compliance.Core/Features/Evaluations/Aggregates/ControlEvaluation.cs` |
| `ControlEvaluationPlan` | Source present; target conformance not verified | P0 (R2-05) | `src/Compliance.Core/Features/Evaluations/Aggregates/ControlEvaluationPlan.cs` |
| `ControlOccurrence` | Source present; target conformance not verified | P0 (R2-04)<br>P1 (T2-02)<br>P2 (T2-09) | `src/Compliance.Core/Features/Operations/Aggregates/ControlOccurrence.cs` |
| `ControlOperatingPlan` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Operations/Aggregates/ControlOperatingPlan.cs` |
| `CorrectiveAction` | Source present; target conformance not verified | P0 (R2-07)<br>P1 (T1-06) | `src/Compliance.Core/Features/Remediation/Aggregates/CorrectiveAction.cs` |
| `CriteriaTextOverlay` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Criteria/Aggregates/CriteriaTextOverlay.cs` |
| `CriterionApplicability` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/ControlMappings/Aggregates/CriterionApplicability.cs` |
| `DataFlow` | Source present; target conformance not verified | P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/DataFlow.cs` |
| `DeclaredApplication` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/DeclaredApplication.cs` |
| `DeclaredSystemInstance` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/DeclaredSystemInstance.cs` |
| `EmailAddress` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/EmailAddress.cs` |
| `EngagementManagementAcknowledgement` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/EngagementManagementAcknowledgement.cs` |
| `EvidenceArtifact` | Source present; target conformance not verified | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceArtifact.cs` |
| `EvidenceHandlingPolicy` | Source present; target conformance not verified | P1 (R2-12) | `src/Compliance.Core/Features/Retention/Aggregates/EvidenceHandlingPolicy.cs` |
| `EvidenceRedaction` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceRedaction.cs` |
| `EvidenceRequest` | Source present; target conformance not verified | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceRequest.cs` |
| `EvidenceSupport` | Source present; target conformance not verified | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceSupport.cs` |
| `ExaminationEngagement` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ExaminationEngagement.cs` |
| `ExaminationSignOff` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ExaminationSignOff.cs` |
| `FindingAggregate` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Remediation/Aggregates/FindingAggregate.cs` |
| `FirmProfessionalDutyDesignation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/FirmProfessionalDutyDesignation.cs` |
| `FirmStaffIdentityClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/FirmStaffIdentityClaim.cs` |
| `FirmStaffRecord` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/FirmStaffRecord.cs` |
| `IdentityRecovery` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/IdentityRecovery.cs` |
| `ImmutableSnapshot` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Snapshots/Aggregates/ImmutableSnapshot.cs` |
| `IndependenceRuleCatalog` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleCatalog.cs` |
| `IndependenceRuleRatificationCatalog` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleRatificationCatalog.cs` |
| `InformationAsset` | Source present; target conformance not verified | P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/InformationAsset.cs` |
| `Location` | Source present; target conformance not verified | P0 (R1-02)<br>P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/Location.cs` |
| `LocationNameClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/TechnologyInventory/Services/LocationNameClaim.cs` |
| `ManagementAssertion` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ManagementAssertion.cs` |
| `ManagementRepresentationLetter` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ManagementRepresentationLetter.cs` |
| `Member` | Done (aggregate scope): Jev reviewed; 61 focused tests pass | P0 (R1-04)<br>P0 (R1-15)<br>P0 (R2-06)<br>P2 (T1-03)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessControl/Aggregates/Member.cs` |
| `MethodologyTemplate` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplate.cs` |
| `MethodologyTemplateContribution` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplateContribution.cs` |
| `OperationalProcess` | Focused review (2026-10-27): tenant replay guard verified; 22 named-process tests pass; full catalog conformance remains unqualified | P0 (R1-02)<br>P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/OperationalProcess.cs` |
| `OperationalProcessNameClaim` | Focused review (2026-10-27): cross-tenant claim/release requests are rejected without emitting facts; 2 focused tests pass; full choreography remains unqualified | No direct story mapping | `src/Compliance.Core/Features/TechnologyInventory/Processes/OperationalProcessNameClaim.cs` |
| `PartnerIndependenceDecision` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/PartnerIndependenceDecision.cs` |
| `Person` | Source present; target conformance not verified | P0 (R1-02)<br>P0 (R1-11)<br>P0 (R2-06)<br>P1 (T2-05) | `src/Compliance.Core/Features/Workforce/Aggregates/Person.cs` |
| `PlatformOperatorRoster` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/PlatformOperatorRoster.cs` |
| `Policy` | Source present; target conformance not verified | P0 (R2-02)<br>P0 (R2-10)<br>P1 (R2-12)<br>P1 (T2-02) | `src/Compliance.Core/Features/Policies/Aggregates/Policy.cs` |
| `PolicyCampaignParticipant` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyCampaignParticipant.cs` |
| `PolicyDistributionCampaign` | Source present; target conformance not verified | P0 (R2-10) | `src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyDistributionCampaign.cs` |
| `PopulationSnapshot` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/Snapshots/Aggregates/PopulationSnapshot.cs` |
| `PracticeIndependenceCommitment` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/PracticeIndependenceCommitment.cs` |
| `ProfessionalAssertionIdentityClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/ProfessionalAssertionIdentityClaim.cs` |
| `ProgramReadiness` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Readiness/Aggregates/ProgramReadiness.cs` |
| `ProgramRollover` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Programs/Aggregates/ProgramRollover.cs` |
| `Provider` | Source present; target conformance not verified | P0 (R1-04)<br>P0 (R1-14)<br>P0 (R2-06)<br>P2 (T2-08) | `src/Compliance.Core/Features/Providers/Aggregates/Provider.cs` |
| `ProviderAssuranceGap` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceGap.cs` |
| `ProviderAssuranceReport` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceReport.cs` |
| `ProviderAssuranceReview` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Providers/Aggregates/ProviderAssuranceReview.cs` |
| `ProviderNameClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Providers/Services/ProviderNameClaim.cs` |
| `ProviderRiskAcceptance` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Providers/Aggregates/ProviderRiskAcceptance.cs` |
| `ReadinessAssessment` | Source present; target conformance not verified | P0 (R1-08) | `src/Compliance.Core/Features/Readiness/Aggregates/ReadinessAssessment.cs` |
| `ReadinessGapPlan` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Readiness/Aggregates/ReadinessGapPlan.cs` |
| `RiskDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskDraft.cs` |
| `RiskEvaluation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskEvaluation.cs` |
| `RiskGovernance` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskGovernance.cs` |
| `RiskMethod` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskMethod.cs` |
| `RiskTreatmentAction` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskTreatmentAction.cs` |
| `RiskTreatmentAssertion` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskTreatmentAssertion.cs` |
| `Role` | Done (aggregate scope): Jev reviewed; 77 combined focused tests pass | P0 (R1-04)<br>P0 (R2-06)<br>P2 (T1-03)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessControl/Aggregates/Role.cs` |
| `RolePermission` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/RolePermission.cs` |
| `SeparationOfDutiesWaiver` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/SeparationOfDutiesWaiver.cs` |
| `ServiceEngagementIndependence` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/ServiceEngagementIndependence.cs` |
| `ServiceIdentity` | Source present; target conformance not verified | P0 (R1-02)<br>P0 (R1-11)<br>P0 (R2-06)<br>P1 (T2-05) | `src/Compliance.Core/Features/Workforce/Aggregates/ServiceIdentity.cs` |
| `SignificantChange` | Source present; target conformance not verified | P1 (T2-07) | `src/Compliance.Core/Features/Changes/Aggregates/SignificantChange.cs` |
| `SystemBoundary` | Done (aggregate scope): Jev reviewed; 81 focused tests pass | P0 (R1-02) | `src/Compliance.Core/Features/Boundaries/Aggregates/SystemBoundary.cs` |
| `SystemDescription` | Source present; target conformance not verified | P1 (T1-02) | `src/Compliance.Core/Features/SystemDescription/Aggregates/SystemDescription.cs` |
| `SystemInstanceAccessReviewScope` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/SystemInstanceAccessReviewScope.cs` |
| `Team` | Done (aggregate scope): Jev reviewed; 77 combined focused tests pass | P0 (R1-04)<br>P1 (T2-04) | `src/Compliance.Core/Features/AccessControl/Aggregates/Team.cs` |
| `TeamMember` | Done (aggregate scope): Jev reviewed; 77 combined focused tests pass | P0 (R1-04) | `src/Compliance.Core/Features/AccessControl/Aggregates/TeamMember.cs` |
| `TeamRole` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/TeamRole.cs` |
| `TechnologyComponent` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/TechnologyComponent.cs` |
| `TemplateApplication` | Source present; target conformance not verified | P1 (F1-04) | `src/Compliance.Core/Features/MethodologyTemplates/Aggregates/TemplateApplication.cs` |
| `Tenant` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/Tenant.cs` |
| `TenantIdentityProvider` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/TenantIdentityProvider.cs` |
| `TenantInvitation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/TenantInvitation.cs` |
| `TenantInvitationMailboxClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/TenantInvitationMailboxClaim.cs` |
| `TenantManagerGuard` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/TenantManagerGuard.cs` |
| `TenantSlug` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/TenantSlug.cs` |
| `TrainingRequirement` | Source present; target conformance not verified | P0 (R2-10) | `src/Compliance.Core/Features/PolicyDistribution/Aggregates/TrainingRequirement.cs` |
| `TypeIIObservationCalendar` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Operations/Aggregates/TypeIIObservationCalendar.cs` |
| `TypeIIPeriod` | Source present; target conformance not verified | P1 (T2-01)<br>P1 (T3-01) | `src/Compliance.Core/Features/Operations/Aggregates/TypeIIPeriod.cs` |
| `TypeIIPlan` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/TypeIIPlan.cs` |
| `UserIdentity` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/UserIdentity.cs` |
| `WorkDigestPreference` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Work/Aggregates/WorkDigestPreference.cs` |
| `WorkItemAssignment` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Work/Aggregates/WorkItemAssignment.cs` |
| `WorkRelationship` | Source present; target conformance not verified | P0 (R1-11) | `src/Compliance.Core/Features/Workforce/Aggregates/WorkRelationship.cs` |
| `WorkforceObservationResolution` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Workforce/Aggregates/WorkforceObservationResolution.cs` |
| `WorkforceSourceObservation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Workforce/Aggregates/WorkforceSourceObservation.cs` |
