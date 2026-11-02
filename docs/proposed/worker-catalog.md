# Worker and process catalog

This is a structured capture of selected domain reactors, durable process execution, and host activation. The physical registration plan is checkpointed evidence, not a claim that the selected target has been implemented or qualified.

## Host activation

| Host mode | Source | Lines | Registered work |
| --- | --- | --- | --- |
| API/standalone when hostMode.RunsWorkers() | src/Compliance.App/Program.cs | 414, 416, 418 | portia.AddWorkers(), AddHostedService<ProcessSweep>(), AddHostedService<WorkDigestDispatchBackgroundService>() |
| worker-only app branch | src/Compliance.App/Program.cs | 3158, 3160, 3162 | AddCompliance(...).AddWorkers(), AddHostedService<ProcessSweep>(), AddHostedService<WorkDigestDispatchBackgroundService>() |

The selected process plan says that API-only mode does not activate processing. The worker-running API/standalone modes and worker-only branch both register Portia workers, `ProcessSweep`, and `WorkDigestDispatchBackgroundService`.

## Existing selected reactor registrations

Existing root reactors recorded by this snapshot: **65**.

| Reactor | Workload | Scope | Source events | Selected rows |
| --- | --- | --- | --- | --- |
| TenantRegistrationReactor | TenantRegistration | Global | TenantRegistered, TenantSlugChangeRequested, TenantSlugChanged, TenantSlugSurrenderRequested | S030, S031, S032, S033 |
| TenantInvitationReactor | TenantInvitation | PerTenant | TenantInvitationAccepted | S026, S027, S028, S029 |
| TenantInvitationDeliveryProcessReactor | TenantInvitationDeliveryProcessV1 | PerTenant | TenantMemberInvited | T117, T118, T119 |
| EvidenceArtifactInspectionEffectsReactor | EvidenceArtifactInspectionEffectsV1 | PerTenant | EvidenceArtifactInspected | S000 |
| EmailReservationReactor | EmailReservation | Global | UserIdentityRegistered | S035 |
| EmailChallengeDeliveryReactor | EmailChallengeDeliveryV1 | Global | EmailChallengeIssued, IdentityRecoveryChallengeIssued | T115, T116 |
| IdentityRecoveryAcceptanceReactor | IdentityRecoveryAcceptanceV1 | Global | IdentityRecoveryChallengeClaimed | T041, T042, T043, T044, T045, T056, T057, T058, T097 |
| TenantRbacBootstrapReactor | TenantRbacBootstrap | Global | TenantRegistered | S011, S012, S013, S014, S015, S016 |
| TenantSelfServiceActivationReactor | TenantSelfServiceActivationV1 | PerTenant | TeamMemberAssigned | S034 |
| FirmStaffRegistrationReactor | FirmStaffRegistrationV1 | Global | FirmStaffRegistrationAccepted | T002, T003 |
| ProfessionalDutyChangeReactor | ProfessionalDutyChangeV1 | Global | ProfessionalDutyChangeAccepted | T000, T001, T113, T114 |
| ClientIndependencePreviewReactor | ClientIndependencePreviewV1 | PerTenant | ClientIndependenceEvaluationAdmitted | T106 |
| EngagementPersonalDecisionReactor | EngagementPersonalDecisionV1 | PerTenant | EngagementPersonalDecisionAdmitted | T089, T090, T091, T092, T093, T094, T095, T096, T107, T108, T109, T110, T111, T112 |
| EngagementAcceptanceReactor | EngagementAcceptanceV1 | PerTenant | ServiceEngagementAcceptanceRequested, ServiceEngagementStaffAssignmentRequested | T004, T005, T006, T007, T082, T083, T084, T085, T089, T090, T091, T092, T093, T094, T095, T096 |
| TypeIIRangeReactor | TypeIIRangeV1 | PerTenant | TypeIIPeriodOpeningAccepted, TypeIIPeriodAmendmentAccepted | T053, T054, T055 |
| TypeIIRangeReleaseReactor | TypeIIRangeReleaseV1 | PerTenant | TypeIIPeriodAmendmentCanceled, TypeIIPeriodOpeningRejected, ObservationPeriodCloseFrozen | T055 |
| ControlOccurrenceFanOutReactor | ControlOccurrenceFanOutV1 | PerTenant | DueControlOccurrencesGenerated | T040 |
| ReadinessRunReactor | ReadinessRunV1 | PerTenant | ReadinessAssessmentRequested | T039, T087 |
| ReadinessDecisionReactor | ReadinessDecisionV1 | PerTenant | ReadinessDecisionAccepted, TypeIEntryDecisionAccepted | T065, T066, T074, T075 |
| AccessReviewLaunchFanOutReactor | AccessReviewLaunchFanOutV1 | PerTenant | AccessReviewCampaignLaunched | T008 |
| AccessReviewCompletionReactor | AccessReviewCampaignCompletionV1 | PerTenant | AccessReviewCampaignCompletionAccepted | T059, T060, T073 |
| AccessReviewCompletionSettlementReactor | AccessReviewCompletionSettlementV1 | PerTenant | AccessReviewCampaignCompletionCanceled | T060 |
| AuditPackageBuildProcessReactor | AuditPackageBuildProcessV1 | PerTenant | AuditPackageBuildRequested | T031, T032 |
| AuditPackageDeliveryAdmissionReactor | AuditPackageDeliveryAdmissionV1 | PerTenant | AuditPackageDeliveryAdmitted | T081 |
| FindingClosureReactor | FindingClosureV1 | PerTenant | FindingClosureAccepted | T037, T063, T064 |
| AuditorRequestResolutionReactor | AuditorRequestResolutionV1 | PerTenant | AuditorRequestResolutionAccepted | T035, T061, T062 |
| AuditorResponseMaterializationReactor | AuditorResponseMaterializationV1 | PerTenant | AuditorResponseAdmitted | T033 |
| AuditorCommentMaterializationReactor | AuditorCommentMaterializationV1 | PerTenant | AuditorCommentAdmitted | T034 |
| AuditorSampleSelectionMaterializationReactor | AuditorSampleSelectionMaterializationV1 | PerTenant | AuditorSampleSelectionAdmitted | T105 |
| CorrectiveActionMaterializationReactor | CorrectiveActionMaterializationV1 | PerTenant | CorrectiveActionAdmitted | T036 |
| PolicyParticipantResultReactor | PolicyParticipantResultV1 | PerTenant | PolicyParticipantResultAdmitted | T076 |
| PolicyCampaignClosureReactor | PolicyCampaignClosureV1 | PerTenant | PolicyCampaignClosureAccepted | T038, T067, T068 |
| AccessReviewBulkAdmissionReactor | AccessReviewDecisionBatchAdmissionV1 | PerTenant | AccessReviewDecisionBatchAdmitted | T009, T010, T011, T080, T086 |
| AccessReviewBulkCancellationReactor | AccessReviewBulkCancellationV1 | PerTenant | AccessReviewDecisionBatchCancellationAccepted | T012 |
| AccessReviewBulkCancellationCompletionReactor | AccessReviewBulkCancellationCompletionV1 | PerTenant | AccessReviewDecisionBatchCancellationAccepted | T079 |
| AccessReviewBulkSettlementReactor | AccessReviewBulkSettlementV1 | PerTenant | AccessReviewDecisionBatchPublished, AccessReviewDecisionBatchCanceled | T080 |
| AdvisorFeedbackMaterializationReactor | AdvisorFeedbackMaterializationV1 | PerTenant | AdvisorFeedbackAdmitted | T013 |
| FirmStaffStatusReevaluationReactor | FirmStaffStatusReevaluationV1 | Global | FirmStaffChangeRecorded | S017 |
| AcceptedStaffDirectoryReconciliationReactor | AcceptedStaffDirectoryReconciliationV1 | PerTenant | ServiceEngagementAcceptanceRecorded, ServiceEngagementStaffAssigned | S018 |
| ImportSourceAdmissionReactor | ImportSourceAdmissionV1 | PerTenant | ApplicationImportPlanSealed | T014, T015, T016, T017, T020, T021 |
| ImportCancellationReactor | ImportCancellationV1 | PerTenant | ApplicationImportCancellationAccepted | T018, T019, T020, T021 |
| TenantSlugReactor | TenantSlug | Global | TenantSlugRegistered, TenantSlugRegistrationRejected, TenantSlugSurrendered, TenantSlugSurrenderRejected | S022, S023, S024, S025 |
| RiskMethodReassessmentReactor | RiskMethodReassessmentV1 | PerTenant | RiskMethodVersionPublished | S036 |
| BoundaryRiskReassessmentReactor | BoundaryRiskReassessmentV1 | PerTenant | BoundaryApproved | S037 |
| RiskActionCompletionReviewReactor | RiskActionCompletionReviewV1 | PerTenant | RiskTreatmentActionCompletionReviewAccepted | T071, T072, T078 |
| TeamCleanupReactor | TeamCleanup | PerTenant | TeamDeleted | S010 |
| ProviderChangeAcceptedReactor | ProviderChangeAcceptedV1 | PerTenant | ProviderChangeAccepted | T022, T023, T024 |
| LocationChangeAcceptedReactor | LocationChangeAcceptedV1 | PerTenant | LocationChangeAccepted | T025, T026, T027 |
| OperationalProcessChangeAcceptedReactor | OperationalProcessChangeAcceptedV1 | PerTenant | OperationalProcessChangeAccepted | T028, T029, T030 |
| ProviderReviewReactor | ProviderReviewV1 | PerTenant | ProviderReviewRequested | T069, T070, T077 |
| ProviderRiskAcceptanceLinkReactor | ProviderRiskAcceptanceLinkV1 | PerTenant | ProviderCoverageGapRiskAcceptanceAdmitted | T088 |
| AdministratorWithdrawalReactor | AdministratorWithdrawalV1 | PerTenant | TenantManagerWithdrawalAccepted | T046, T050 |
| AdministratorAssignmentWithdrawalReactor | AdministratorAssignmentWithdrawalV1 | PerTenant | TenantManagerWithdrawalAccepted | T047, T050 |
| AdministratorRestorationReactor | AdministratorRestorationV1 | PerTenant | TenantManagerRestorationAccepted | T051, T052 |
| AdministratorAssignmentRestorationReactor | AdministratorAssignmentRestorationV1 | PerTenant | TenantManagerRestorationAccepted | T052, T098 |
| AdministratorNeutralMemberMutationReactor | AdministratorNeutralMemberMutationV1 | PerTenant | TenantManagerSourceMutationAdmitted | T048 |
| AdministratorNeutralAssignmentMutationReactor | AdministratorNeutralAssignmentMutationV1 | PerTenant | TenantManagerSourceMutationAdmitted | T049 |
| MemberRetirementReactor | MemberRetirementV1 | PerTenant | MemberDeprovisioned | S003, S004, S005 |
| RetiredTeamAssignmentReactor | RetiredTeamAssignmentV1 | PerTenant | TeamMemberAssigned | S007 |
| RetiredMemberGrantReactor | RetiredMemberGrantV1 | PerTenant | AccessGrantIssued | S006 |
| RoleCleanupReactor | RoleCleanup | PerTenant | RoleDeleted | S008, S009 |
| ControlOccurrenceFindingReactor | ControlOccurrenceFindingsV1 | PerTenant | ControlOccurrenceAttested, ControlOccurrenceReviewed | S002 |
| LinkImpactReviewWorkReactor | SignificantChangeImpactWorkLinksV2 | PerTenant | SignificantChangeImpactAssessed | S019 |
| SystemDescriptionDecisionReactor | SystemDescriptionImpactDecisionsV1 | PerTenant | SystemDescriptionApproved, SystemDescriptionNoChangeRecorded | S020, S021 |
| ControlEvaluationDeviationFindingReactor | ControlEvaluationDeviationFindingsV1 | PerTenant | ControlEvaluationSubmitted | S001 |

## Planned root starters

The selected target adds two root starters with new workload identities; implementation remains unauthorized/unimplemented in the source plan.

| Role | Reactor | Workload | Scope | Source events | Status |
| --- | --- | --- | --- | --- | --- |
| one committed EvidenceArtifactDownloadRequested@v1 root subscriber | ArtifactDownloadRequestedReactor | ArtifactDownloadRequestedV1 | WorkloadScope.PerTenant | exact tenant EvidenceArtifact area | UNIMPLEMENTED_REQUIRED_TARGET |
| one committed ProgramRolloverExecutionClaimed root subscriber | ProgramRolloverExecutionClaimedReactor | ProgramRolloverExecutionClaimedV1 | WorkloadScope.PerTenant | exact tenant program-rollovers area | UNIMPLEMENTED_REQUIRED_TARGET |

## Durable process runtime

| Component or rule | Selected detail |
| --- | --- |
| Common components | ProcessRootReaction, ProcessCatalog, ProcessDriver, IProcessRecordStore/FitzKvProcessRecordStore, ProcessSweep |
| Root admission | Persist exact process record and frozen complete targets before reactor checkpoint advance; thereafter driver and worker sweep recover independently. |
| Driver | Stable root/receiver/target/episode command identity; lease + record CAS; record actual produced source fact; transient retry, invalid-proof quarantine, explicit abort fences before settlement. A crash after append before CAS replays the same command. |
| Worker activation rule | Compliance.App Program.cs AddWorkers plus hosted ProcessSweep only in worker-running modes; API-only registration does not activate processing. |
| Existing process definitions | 28 |
| Planned process definitions | 2 |
| Selected total definitions | 30 |
| Additional driver types | 0 |
| Additional hosted worker types | 0 |

Existing registered definitions: AuditPackageBuildProcess, AuditPackageDeliveryRecordingProcess, AdministratorTransitionProcess, IdentityRecoveryProcess, EmailChallengeDeliveryProcess, FirmStaffRegistrationProcess, ClientIndependencePreviewProcess, EngagementPersonalDecisionProcess, EngagementAcceptanceProcess, ProfessionalDutyChangeProcess, TypeIIRangeProcess, ControlOccurrenceFanOut, ReadinessRunProcess, ReadinessDecisionProcess, ApplicationImportExecutionProcess, AccessReviewLaunchFanOut, AccessReviewCompletionProcess, BulkAccessDecisionProcess, FindingClosureProcess, AuditorRequestResolutionProcess, PolicyCampaignClosureProcess, RiskActionCompletionReviewProcess, TenantInvitationDeliveryProcess, ProviderNameChangeProcess, LocationNameChangeProcess, OperationalProcessNameChangeProcess, ProviderReviewProcess, IndependentChildMaterializationProcess.

Planned definitions: ArtifactDownloadProcess, ProgramRolloverExecutionProcess.

Process identities, ordered stages, and aliases: [process catalog](processes/README.md) and `{"AccessReviewCampaignCompletionProcess":"AccessReviewCompletionProcess","IndependentChildMaterialization":"IndependentChildMaterializationProcess"}`.

## Other technical orchestrations

| Type | Entry | Authority | Recovery |
| --- | --- | --- | --- |
| SystemDescriptionExportProcess | GenerateSystemDescriptionExport handler, internal; no added public route/MCP and no ExportGenerated domain event | Freezes exact independently approved description version/content/source/approval into immutable intent; own technical CAS record and artifact output; accepted retry never reselects newer source. | Completed original technical result replays; incomplete same accepted record resumes on invocation. Not a ProcessDefinition or ProcessSweep entry. |
| WorkDigestDispatchBackgroundService | Hosted only in worker-running modes and only when delivery settings ready | Current tenant/member discovery, local-time due schedule, durable per-member/week dispatch state and exact attempt claim; external outcome recorded on actual WorkDigestDispatch owner. This is distinct from typed WeeklyWorkDigest Schedule/Deliver commands. | Pending dispatch resumes before considering a new schedule; unknown transport remains unknown and is not automatically resent; source state and attempt OCC fence competing workers. |
| ScheduleWeeklyWorkDigest | Registered named system scheduler command; no public HTTP/MCP or new scheduler worker selected | Required authoritative complete captured tenant/week/ScheduleKey plan, original page result and terminal actual scheduled proofs; current handler is only implementation evidence and fails qualified old-API regressions. | [Placeholder: value not recorded] |

## Runtime evidence and open details

Process infrastructure source records: src/Compliance.Core/Features/Processes/ProcessRootReaction.cs (90619f9b06bc1a4cec7eeb8f7e241ff701c88b100265e83313682fcd047cf383), src/Compliance.Core/Features/Processes/ProcessDriver.cs (caf9efd04bfbe35fd179564ae092e3fcf12e84997911b77dbf8f6a1c28dfd6c3), src/Compliance.Core/Features/Processes/ProcessSweep.cs (195efcb4b2ba8187fa92994f7637214fc219753fa0fb7d11173ac2348228b6e1), src/Compliance.Core/Features/Processes/ProcessCatalog.cs (a486d675a44e96c54f20dc856ffb7603f355e833664b9ce0c3a9ca20a8bf28c1).

Current selected process definitions: 30; the physical plan records 28 existing registered definitions and 2 planned definitions.

[Placeholder: worker polling intervals, process timeout policy, cross-host singleton guarantees, operational alerting, and operator runbooks are not consolidated in the selected worker catalog.]

Complete physical plan snapshot: [JSON](data/physical-registration-plan-snapshot.json).
