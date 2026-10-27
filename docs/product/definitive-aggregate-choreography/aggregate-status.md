# Aggregate implementation status and priority

Generated from the definitive aggregate catalog (129 business/authority aggregates), the current source tree, and the product backlog on 2026-10-27.

The catalog is the authority for the intended aggregate set. It cautions that source presence does not establish target conformance. “Source present” therefore means a source file exists; it does not claim the aggregate satisfies every selected invariant or has passed full qualification. ComplianceProgram, SystemBoundary, ClientService, Member, Role, Team and TeamMember are marked done for aggregate scope based on their focused tests and Jev review; repository-wide qualification remains separate.

Priority is mapped only when an aggregate is named in a backlog story's body; ComplianceProgram is explicitly mapped to R1-01 because that story owns the program plan and continuing identity. “No direct story mapping” means the backlog does not name the aggregate in a story body; it does not mean the aggregate has no product value.

| Aggregate | Current implementation status | Backlog priority | Current source |
| --- | --- | --- | --- |
| `AccessExpectation` | No direct source path; implementation mapping needed | P0 (R2-06)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessReviews/AccessReviewSystemLedger.cs` |
| `AccessGrant` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessControl/Aggregates/AccessGrant.cs` |
| `AccessPopulation` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulation.cs` |
| `AccessPopulationException` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/AccessReviewSystemLedger.cs` |
| `AccessPrincipalClassification` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessPopulation.cs` |
| `AccessReviewCampaign` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewCampaign.cs` |
| `AccessReviewDecisionBatch` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewCampaign.cs` |
| `AccessReviewItem` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/AccessReviews/Aggregates/AccessReviewCampaign.cs` |
| `AdvisorFeedbackThread` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AdvisorCollaboration/Aggregates/AdvisorHandoff.cs` |
| `AdvisorHandoff` | Source present; target conformance not verified | P0 (R2-08) | `src/Compliance.Core/Features/AdvisorCollaboration/Aggregates/AdvisorHandoff.cs` |
| `ApplicationImportBatch` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Applications/ApplicationImportLedger.cs` |
| `ApplicationImportSource` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Applications/ApplicationImportLedger.cs` |
| `ApplicationRelationship` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/DeclaredApplication.cs` |
| `ArtifactDisclosure` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisclosure.cs` |
| `ArtifactDisposition` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactDisposition.cs` |
| `ArtifactDownloadGrant` | No direct source path; implementation mapping needed | No direct story mapping | — |
| `ArtifactEngagementHold` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Retention/Aggregates/ArtifactEngagementHold.cs` |
| `ArtifactLegalHold` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Retention/ArtifactRetention.cs` |
| `ArtifactRetentionBasis` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Retention/ArtifactRetention.cs` |
| `AuditObservation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/AuditObservation.cs` |
| `AuditOutcome` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/AuditOutcome.cs` |
| `AuditPackageDelivery` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AuditPackages/AuditPackage.cs` |
| `AuditPackageManifest` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AuditPackages/AuditPackage.cs` |
| `AuditPopulation` | Source present; target conformance not verified | P1 (T3-02) | `src/Compliance.Core/Features/AuditPopulations/Aggregates/AuditPopulation.cs` |
| `AuditorFeedbackThread` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs` |
| `AuditorRequest` | Source present; target conformance not verified | P1 (T1-04)<br>P1 (T3-03) | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs` |
| `AuditorResponse` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs` |
| `AuditorSampleSelection` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AuditorRequests/Aggregates/AuditorRequest.cs` |
| `ClientExport` | Source present; target conformance not verified | P1 (F1-01) | `src/Compliance.Core/Features/Exports/Aggregates/ClientExport.cs` |
| `ClientIndependenceDecision` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
| `ClientService` | Done (aggregate scope): Jev reviewed; 35 focused tests pass | P0 (R1-02) | `src/Compliance.Core/Features/Programs/Aggregates/ClientService.cs` |
| `ClientServiceIndependence` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
| `CommitmentDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Commitments/Aggregates/CommitmentDraft.cs` |
| `ComplianceIncident` | Source present; target conformance not verified | P1 (T2-07) | `src/Compliance.Core/Features/Incidents/Aggregates/ComplianceIncident.cs` |
| `ComplianceProgram` | Done: Jev reviewed; 116 focused tests pass | P0 (R1-01) | `src/Compliance.Core/Features/Programs/Aggregates/ComplianceProgram.cs` |
| `ControlCriterionMapping` | No direct source path; implementation mapping needed | P0 (R1-06) | `src/Compliance.Core/Features/ControlMappings/ControlCriterionMappingLedger.cs` |
| `ControlDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Controls/Aggregates/ControlDraft.cs` |
| `ControlEvaluation` | No direct source path; implementation mapping needed | P0 (R2-05) | `src/Compliance.Core/Features/Evaluations/ControlEvaluationLedger.cs` |
| `ControlEvaluationPlan` | Source present; target conformance not verified | P0 (R2-05) | `src/Compliance.Core/Features/Evaluations/Aggregates/ControlEvaluationPlan.cs` |
| `ControlOccurrence` | No direct source path; implementation mapping needed | P0 (R2-04)<br>P1 (T2-02)<br>P2 (T2-09) | `src/Compliance.Core/Features/Operations/ControlOperationsLedger.cs` |
| `ControlOperatingPlan` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Operations/ControlOperationsLedger.cs` |
| `CorrectiveAction` | Source present; target conformance not verified | P0 (R2-07)<br>P1 (T1-06) | `src/Compliance.Core/Features/Remediation/Aggregates/FindingAggregate.cs` |
| `CriteriaTextOverlay` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Criteria/CriteriaTextOverlayLedger.cs` |
| `CriterionApplicability` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/ControlMappings/CriterionApplicabilityLedger.cs` |
| `DataFlow` | Source present; target conformance not verified | P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/DataFlow.cs` |
| `DeclaredApplication` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/DeclaredApplication.cs` |
| `DeclaredSystemInstance` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Applications/Aggregates/DeclaredSystemInstance.cs` |
| `EmailAddress` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/EmailAddress.cs` |
| `EngagementManagementAcknowledgement` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
| `EvidenceArtifact` | Source present; target conformance not verified | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceArtifact.cs` |
| `EvidenceHandlingPolicy` | Source present; target conformance not verified | P1 (R2-12) | `src/Compliance.Core/Features/Retention/Aggregates/EvidenceHandlingPolicy.cs` |
| `EvidenceRedaction` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceRedaction.cs` |
| `EvidenceRequest` | No direct source path; implementation mapping needed | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/EvidenceRequestLedger.cs` |
| `EvidenceSupport` | Source present; target conformance not verified | P0 (R2-03) | `src/Compliance.Core/Features/Evidence/Aggregates/EvidenceSupport.cs` |
| `ExaminationEngagement` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ExaminationEngagement.cs` |
| `ExaminationSignOff` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ExaminationSignOff.cs` |
| `FindingAggregate` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Remediation/Aggregates/FindingAggregate.cs` |
| `FirmProfessionalDutyDesignation` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/FirmProfessionalDutyCatalog.cs` |
| `FirmStaffIdentityClaim` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/FirmStaffDirectory.cs` |
| `FirmStaffRecord` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/FirmStaffDirectory.cs` |
| `IdentityRecovery` | No direct source path; implementation mapping needed | No direct story mapping | — |
| `ImmutableSnapshot` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Snapshots/Aggregates/ImmutableSnapshot.cs` |
| `IndependenceRuleCatalog` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleCatalog.cs` |
| `IndependenceRuleRatificationCatalog` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/IndependenceRuleRatificationCatalog.cs` |
| `InformationAsset` | Source present; target conformance not verified | P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/Aggregates/InformationAsset.cs` |
| `Location` | No direct source path; implementation mapping needed | P0 (R1-02)<br>P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/LocationRegister.cs` |
| `LocationNameClaim` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/TechnologyInventory/LocationRegister.cs` |
| `ManagementAssertion` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ManagementAssertion.cs` |
| `ManagementRepresentationLetter` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/ManagementRepresentationLetter.cs` |
| `Member` | Done (aggregate scope): Jev reviewed; 61 focused tests pass | P0 (R1-04)<br>P0 (R1-15)<br>P0 (R2-06)<br>P2 (T1-03)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessControl/Aggregates/Member.cs` |
| `MethodologyTemplate` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplate.cs` |
| `MethodologyTemplateContribution` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/MethodologyTemplates/Aggregates/MethodologyTemplateContribution.cs` |
| `OperationalProcess` | No direct source path; implementation mapping needed | P0 (R1-02)<br>P0 (R1-12) | `src/Compliance.Core/Features/TechnologyInventory/OperationalProcessRegister.cs` |
| `OperationalProcessNameClaim` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/TechnologyInventory/OperationalProcessRegister.cs` |
| `PartnerIndependenceDecision` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
| `Person` | Source present; target conformance not verified | P0 (R1-02)<br>P0 (R1-11)<br>P0 (R2-06)<br>P1 (T2-05) | `src/Compliance.Core/Features/Workforce/Aggregates/Person.cs` |
| `PlatformOperatorRoster` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/PlatformOperatorRoster.cs` |
| `Policy` | Source present; target conformance not verified | P0 (R2-02)<br>P0 (R2-10)<br>P1 (R2-12)<br>P1 (T2-02) | `src/Compliance.Core/Features/Policies/Aggregates/Policy.cs` |
| `PolicyCampaignParticipant` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyDistributionCampaign.cs` |
| `PolicyDistributionCampaign` | Source present; target conformance not verified | P0 (R2-10) | `src/Compliance.Core/Features/PolicyDistribution/Aggregates/PolicyDistributionCampaign.cs` |
| `PopulationSnapshot` | Source present; target conformance not verified | P0 (R2-06) | `src/Compliance.Core/Features/Snapshots/Aggregates/PopulationSnapshot.cs` |
| `PracticeIndependenceCommitment` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
| `ProfessionalAssertionIdentityClaim` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/FirmProfessionalDutyCatalog.cs` |
| `ProgramReadiness` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Readiness/ReadinessLedger.cs` |
| `ProgramRollover` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Programs/Aggregates/ProgramRollover.cs` |
| `Provider` | No direct source path; implementation mapping needed | P0 (R1-04)<br>P0 (R1-14)<br>P0 (R2-06)<br>P2 (T2-08) | `src/Compliance.Core/Features/Providers/ProviderRegister.cs` |
| `ProviderAssuranceGap` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs` |
| `ProviderAssuranceReport` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs` |
| `ProviderAssuranceReview` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs` |
| `ProviderNameClaim` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Providers/ProviderRegister.cs` |
| `ProviderRiskAcceptance` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs` |
| `ReadinessAssessment` | No direct source path; implementation mapping needed | P0 (R1-08) | `src/Compliance.Core/Features/Readiness/ReadinessLedger.cs` |
| `ReadinessGapPlan` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Readiness/ReadinessLedger.cs` |
| `RiskDraft` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskDraft.cs` |
| `RiskEvaluation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskEvaluation.cs` |
| `RiskGovernance` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs` |
| `RiskMethod` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Risks/Aggregates/RiskMethod.cs` |
| `RiskTreatmentAction` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs` |
| `RiskTreatmentAssertion` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs` |
| `Role` | Done (aggregate scope): Jev reviewed; 77 combined focused tests pass | P0 (R1-04)<br>P0 (R2-06)<br>P2 (T1-03)<br>P1 (T2-05) | `src/Compliance.Core/Features/AccessControl/Aggregates/Role.cs` |
| `RolePermission` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/RolePermission.cs` |
| `SeparationOfDutiesWaiver` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/SeparationOfDutiesWaiver.cs` |
| `ServiceEngagementIndependence` | No direct source path; implementation mapping needed | No direct story mapping | `src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs` |
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
| `TenantInvitationMailboxClaim` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/TenantInvitation.cs` |
| `TenantManagerGuard` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/AccessControl/Aggregates/TenantManagerGuard.cs` |
| `TenantSlug` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Tenants/Aggregates/TenantSlug.cs` |
| `TrainingRequirement` | No direct source path; implementation mapping needed | P0 (R2-10) | `src/Compliance.Core/Features/PolicyDistribution/TrainingCatalog.cs` |
| `TypeIIObservationCalendar` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Operations/Aggregates/TypeIIObservationCalendar.cs` |
| `TypeIIPeriod` | Source present; target conformance not verified | P1 (T2-01)<br>P1 (T3-01) | `src/Compliance.Core/Features/Operations/Aggregates/TypeIIPeriod.cs` |
| `TypeIIPlan` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Examinations/Aggregates/TypeIIPlan.cs` |
| `UserIdentity` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/UserIdentities/Aggregates/UserIdentity.cs` |
| `WorkDigestPreference` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Work/Aggregates/WorkDigestPreference.cs` |
| `WorkItemAssignment` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Work/Aggregates/WorkItemAssignment.cs` |
| `WorkRelationship` | Source present; target conformance not verified | P0 (R1-11) | `src/Compliance.Core/Features/Workforce/Aggregates/WorkRelationship.cs` |
| `WorkforceObservationResolution` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Workforce/Aggregates/WorkforceObservationResolution.cs` |
| `WorkforceSourceObservation` | Source present; target conformance not verified | No direct story mapping | `src/Compliance.Core/Features/Workforce/Aggregates/WorkforceSourceObservation.cs` |
