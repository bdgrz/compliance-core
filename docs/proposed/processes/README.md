# Process definitions

Selected process definitions: **30**.

| Process | Root source events | Ordered |
| --- | --- | --- |
| [ProfessionalDutyChangeProcess](professional-duty-change-process.md) | ProfessionalDutyChangeAccepted | true |
| [FirmStaffRegistrationProcess](firm-staff-registration-process.md) | FirmStaffRegistrationAccepted | true |
| [EngagementAcceptanceProcess](engagement-acceptance-process.md) | ServiceEngagementAcceptanceRequested, ServiceEngagementStaffAssignmentRequested | true |
| [EngagementPersonalDecisionProcess](engagement-personal-decision-process.md) | EngagementPersonalDecisionAdmitted | true |
| [ClientIndependencePreviewProcess](client-independence-preview-process.md) | ClientIndependenceEvaluationAdmitted | false |
| [AccessReviewLaunchFanOut](access-review-launch-fan-out.md) | AccessReviewCampaignLaunched | false |
| [BulkAccessDecisionProcess](bulk-access-decision-process.md) | AccessReviewDecisionBatchAdmitted | true |
| [AccessReviewCampaignCompletionProcess](access-review-campaign-completion-process.md) | AccessReviewCampaignCompletionAccepted | true |
| [AuditorRequestResolutionProcess](auditor-request-resolution-process.md) | AuditorRequestResolutionAccepted | true |
| [FindingClosureProcess](finding-closure-process.md) | FindingClosureAccepted | true |
| [ReadinessDecisionProcess](readiness-decision-process.md) | ReadinessDecisionAccepted, TypeIEntryDecisionAccepted | true |
| [PolicyCampaignClosureProcess](policy-campaign-closure-process.md) | PolicyCampaignClosureAccepted | true |
| [ProviderReviewProcess](provider-review-process.md) | ProviderReviewRequested | true |
| [RiskActionCompletionReviewProcess](risk-action-completion-review-process.md) | RiskTreatmentActionCompletionReviewAccepted | true |
| [ApplicationImportExecutionProcess](application-import-execution-process.md) | ApplicationImportPlanSealed | true |
| [ProviderNameChangeProcess](provider-name-change-process.md) | ProviderChangeAccepted | true |
| [LocationNameChangeProcess](location-name-change-process.md) | LocationChangeAccepted | true |
| [OperationalProcessNameChangeProcess](operational-process-name-change-process.md) | OperationalProcessChangeAccepted | true |
| [AuditPackageBuildProcess](audit-package-build-process.md) | AuditPackageBuildRequested | true |
| [AuditPackageDeliveryRecordingProcess](audit-package-delivery-recording-process.md) | AuditPackageDeliveryAdmitted | false |
| [ReadinessRunProcess](readiness-run-process.md) | ReadinessAssessmentRequested | true |
| [IdentityRecoveryProcess](identity-recovery-process.md) | IdentityRecoveryChallengeClaimed | true |
| [AdministratorTransitionProcess](administrator-transition-process.md) | TenantManagerWithdrawalAccepted, TenantManagerRestorationAccepted, TenantManagerSourceMutationAdmitted | true |
| [TypeIIRangeProcess](type-ii-range-process.md) | TypeIIPeriodOpeningAccepted, TypeIIPeriodAmendmentAccepted | true |
| [ArtifactDownloadProcess](artifact-download-process.md) | EvidenceArtifactDownloadRequested | true |
| [ControlOccurrenceFanOut](control-occurrence-fan-out.md) | DueControlOccurrencesGenerated | false |
| [IndependentChildMaterialization](independent-child-materialization.md) | AdvisorFeedbackAdmitted, AuditorResponseAdmitted, AuditorCommentAdmitted, CorrectiveActionAdmitted, AuditorSampleSelectionAdmitted, ProviderCoverageGapRiskAcceptanceAdmitted, PolicyParticipantResultAdmitted | false |
| [EmailChallengeDeliveryProcess](email-challenge-delivery-process.md) | EmailChallengeIssued, IdentityRecoveryChallengeIssued | true |
| [TenantInvitationDeliveryProcess](tenant-invitation-delivery-process.md) | TenantMemberInvited | true |
| [ProgramRolloverExecutionProcess](program-rollover-execution-process.md) | ProgramRolloverExecutionClaimed | true |

ProgramRollover execution narrative and selected stages: [program-rollover-execution-process.md](program-rollover-execution-process.md).
