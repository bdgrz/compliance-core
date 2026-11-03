# Reactors and choreography

Selected architecture rows: **196**. Each row preserves source, target, trigger, required proofs, effect identity, recovery, and implementation-conformance details in JSON.

| Root | Rows |
| --- | --- |
| [ReassessStaffIndependenceAfterChangesReactor](accepted-staff-directory-reconciliation-reactor.md) | 1 |
| [PublishApprovedAccessReviewDecisionReactor](access-review-bulk-admission-reactor.md) | 5 |
| [CompleteAccessReviewBatchCancellationReactor](access-review-bulk-cancellation-completion-reactor.md) | 1 |
| [CancelUnpublishedAccessReviewDecisionsReactor](access-review-bulk-cancellation-reactor.md) | 1 |
| [ApplyPublishedAccessReviewDecisionReactor](access-review-bulk-settlement-reactor.md) | 1 |
| [AccessReviewCompletionReactor](access-review-completion-reactor.md) | 3 |
| [ApplyAccessReviewCampaignOutcomeToItemsReactor](access-review-completion-settlement-reactor.md) | 1 |
| [OpenAccessReviewWorkReactor](access-review-launch-fan-out-reactor.md) | 1 |
| [AdministratorAssignmentRestorationReactor](administrator-assignment-restoration-reactor.md) | 2 |
| [AdministratorAssignmentWithdrawalReactor](administrator-assignment-withdrawal-reactor.md) | 2 |
| [RemoveUnneededAdministratorAssignmentReactor](administrator-neutral-assignment-mutation-reactor.md) | 1 |
| [ApplyAcceptedMemberStatusChangeReactor](administrator-neutral-member-mutation-reactor.md) | 1 |
| [AdministratorRestorationReactor](administrator-restoration-reactor.md) | 2 |
| [AdministratorWithdrawalReactor](administrator-withdrawal-reactor.md) | 2 |
| [PreserveAdvisorFeedbackAfterHandoffReactor](advisor-feedback-materialization-reactor.md) | 1 |
| [KeepApplicationNamesUniqueReactor](application-name-change-reactor.md) | 3 |
| [RemoveRelationshipsAfterApplicationRetirementReactor](application-retirement-reactor.md) | 1 |
| [ProtectArtifactsUnderLegalHoldReactor](artifact-legal-hold-reactor.md) | 1 |
| [PreserveAuditorCommentsAfterRequestReactor](auditor-comment-materialization-reactor.md) | 1 |
| [AuditorRequestResolutionReactor](auditor-request-resolution-reactor.md) | 3 |
| [PreserveAuditorResponseDraftAfterRequestReactor](auditor-response-materialization-reactor.md) | 1 |
| [RecordAuditorSampleSelectionReactor](auditor-sample-selection-materialization-reactor.md) | 1 |
| [BuildAuditPackageForReviewReactor](audit-package-build-process-reactor.md) | 2 |
| [RecordAuditPackageDeliveryReactor](audit-package-delivery-admission-reactor.md) | 1 |
| [BoundaryRiskReassessmentReactor](boundary-risk-reassessment-reactor.md) | 1 |
| [KeepEngagementEvidenceProtectedAfterDeliveryReactor](client-export-delivery-reactor.md) | 1 |
| [EvaluateClientIndependenceForAcceptanceReactor](client-independence-preview-reactor.md) | 1 |
| [ControlEvaluationDeviationFindingReactor](control-evaluation-deviation-finding-reactor.md) | 1 |
| [OpenDueControlWorkReactor](control-occurrence-fan-out-reactor.md) | 1 |
| [ControlOccurrenceFindingReactor](control-occurrence-finding-reactor.md) | 1 |
| [OpenCorrectiveActionForFindingReactor](corrective-action-materialization-reactor.md) | 1 |
| [SendAccountVerificationAndRecoveryEmailsReactor](email-challenge-delivery-reactor.md) | 4 |
| [KeepTenantEmailAddressesUniqueReactor](email-reservation-reactor.md) | 1 |
| [EngagementAcceptanceReactor](engagement-acceptance-reactor.md) | 16 |
| [RecordEngagementIndependenceDecisionsReactor](engagement-personal-decision-reactor.md) | 14 |
| [ApplyArtifactInspectionOutcomeReactor](evidence-artifact-inspection-effects-reactor.md) | 1 |
| [FindingClosureReactor](finding-closure-reactor.md) | 3 |
| [FirmStaffRegistrationReactor](firm-staff-registration-reactor.md) | 2 |
| [ReassessFirmStaffIndependenceAfterChangesReactor](firm-staff-status-reevaluation-reactor.md) | 1 |
| [CompleteAccountRecoveryReactor](identity-recovery-acceptance-reactor.md) | 9 |
| [CancelApplicationImportReactor](import-cancellation-reactor.md) | 4 |
| [ApplyAcceptedApplicationImportReactor](import-source-admission-reactor.md) | 7 |
| [RequestImpactReviewAfterSignificantChangeReactor](link-impact-review-work-reactor.md) | 1 |
| [KeepLocationNamesUniqueReactor](location-change-accepted-reactor.md) | 3 |
| [RemoveMemberAssignmentsAndAccessAfterMembershipEndsReactor](member-retirement-reactor.md) | 3 |
| [FulfillAuthorizedArtifactDownloadRequestReactor](one-committed-evidence-artifact-download-requested-starter.md) | 6 |
| [StartApprovedProgramRolloverReactor](one-new-committed-execution-claim-starter.md) | 1 |
| [KeepOperationalProcessNamesUniqueReactor](operational-process-change-accepted-reactor.md) | 4 |
| [PolicyCampaignClosureReactor](policy-campaign-closure-reactor.md) | 3 |
| [RecordPolicyRequirementOutcomesReactor](policy-participant-result-reactor.md) | 1 |
| [ApplyOrRevokeProfessionalDutyDesignationReactor](professional-duty-change-reactor.md) | 4 |
| [KeepProviderNamesUniqueReactor](provider-change-accepted-reactor.md) | 4 |
| [RecordProviderAssuranceReportReviewReactor](provider-review-reactor.md) | 3 |
| [KeepRiskAcceptanceWithProviderGapReactor](provider-risk-acceptance-link-reactor.md) | 1 |
| [RecordProgramReadinessAndTypeIDecisionsReactor](readiness-decision-reactor.md) | 4 |
| [ReassessProgramReadinessReactor](readiness-run-reactor.md) | 2 |
| [RevokeAccessAfterMemberRetirementReactor](retired-member-grant-reactor.md) | 1 |
| [RemoveTeamAssignmentAfterMemberRetirementReactor](retired-team-assignment-reactor.md) | 1 |
| [RiskActionCompletionReviewReactor](risk-action-completion-review-reactor.md) | 3 |
| [RequestRiskReassessmentWhenRiskChangesReactor](risk-evaluation-reactor.md) | 1 |
| [RiskMethodReassessmentReactor](risk-method-reassessment-reactor.md) | 1 |
| [RemoveRoleAssignmentsAfterRoleDeletionReactor](role-cleanup-reactor.md) | 2 |
| [UpdateSystemDescriptionAfterSignificantChangeReactor](system-description-decision-reactor.md) | 2 |
| [RemoveTeamAssignmentsAfterTeamDeletionReactor](team-cleanup-reactor.md) | 1 |
| [InviteMemberToJoinTenantReactor](tenant-invitation-delivery-process-reactor.md) | 3 |
| [ActivateMembershipAfterInvitationAcceptanceReactor](tenant-invitation-reactor.md) | 4 |
| [SetUpStartingTenantAccessReactor](tenant-rbac-bootstrap-reactor.md) | 6 |
| [EstablishTenantAndInviteOwnerReactor](tenant-registration-reactor.md) | 4 |
| [TenantSelfServiceActivationReactor](tenant-self-service-activation-reactor.md) | 1 |
| [KeepTenantWebAddressesUniqueReactor](tenant-slug-reactor.md) | 4 |
| [ReserveAcceptedTypeIIPeriodDatesReactor](type-ii-range-reactor.md) | 3 |
| [ReleaseTypeIIPeriodDateReservationReactor](type-ii-range-release-reactor.md) | 1 |

Rows without a root name: [unassigned architecture rows](unassigned-architecture-rows.md).
Program rollover: [process page](../processes/program-rollover-execution-process.md).
Physical registration snapshot: [registration-plan.md](registration-plan.md).
