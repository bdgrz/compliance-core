# Write model names

This is the naming target for commands → events → reactors. The complete target names appear on the generated command, event, and reactor pages; this page records the naming rules and every changed name.

## Naming rules

- Commands begin with the action the person or business process asks the system to take.
- Events name a completed fact in past tense.
- Reactor names state the business outcome that justifies the reaction; Reactor is the system role suffix.
- Formal compliance terms remain when they are part of the business vocabulary. Mechanism terms such as materialize, fence, neutral mutation, bootstrap, fan out, cleanup, effect, chunk, and execution claim do not name the target business action or fact.
- Business event names omit implementation version suffixes; the selected source type, discriminator, and discriminator version remain visible as compatibility evidence.
- Target names do not change the selected source name, persisted event discriminator, discriminator version, or command identity. Those remain compatibility evidence for implementation planning.

## Coverage

| Write model element | Target names |
| --- | --- |
| Commands | 503 |
| Events | 501 |
| Named reactor roots | 72 |

Selected command-contract entries: 507; distinct source command names: 503. Repeated source-command entries under multiple owners share their target name. Each distinct command, event, and reactor root has a unique target name. Every named reactor root has a stated business reason. Names not listed as changed retain their selected source name. Repeated authorization command/event names follow the suffix rules recorded in [the structured naming decisions](data/write-model-names.json).

## Commands

| Selected source name | Target command name | Why it names the requested action |
| --- | --- | --- |
| `MaterializeAccessReviewDecisionBatch` | `RecordAccessReviewDecisionBatch` | Records the already admitted exact bulk selection without rebuilding or changing it. |
| `MaterializeAccessReviewItem` | `OpenAccessReviewItem` | Opens one assigned review item for each selected item and reviewer. |
| `FenceAccessReviewItemDecision` | `CancelAccessReviewItemDecision` | Records cancellation of delayed decision application before item reservations are released. |
| `AuthorizeAccessReviewItemCampaignCompletionUse` | `AuthorizeAccessReviewItemCampaignCompletion` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleAccessReviewItemCampaignCompletionUse` | `CloseAccessReviewItemCampaignCompletionAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `SettleAccessReviewItemDecision` | `CloseAccessReviewItemDecisionAuthorization` | Ends the exact item decision authorization after the batch is published or canceled. |
| `MaterializeAdvisorFeedback` | `RecordAdvisorHandoffFeedback` | Records the exact feedback submitted for an advisor handoff, preserving its original actor and referenced record version. |
| `RecordAdvisorFeedback` | `SubmitAdvisorFeedback` | Names the advisor's action to submit feedback for review and admission against the handoff. |
| `AuthorizeAdvisorArtifactDownloadUse` | `AuthorizeAdvisorArtifactDownload` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleAdvisorArtifactDownloadUse` | `CloseAdvisorArtifactDownloadAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `CorrelateApplicationImportRow` | `RecordApplicationImportRowChoice` | Records the person's choice to link the row to an existing application or create a new one. |
| `FenceArtifactDispositionForHold` | `StopArtifactDisposalForHold` | Stops an approved disposal when a new legal or engagement hold applies before deletion finishes. |
| `RecordEngagementReportIssued` | `RecordEngagementReportIssuance` | Records the exact auditor report and issue date that start the evidence preservation period. |
| `RecordEngagementPackageDelivered` | `RecordEngagementPackageDelivery` | Records delivery of the exact engagement package before its preservation hold can be released. |
| `MaterializeRecordedAuditPackageDelivery` | `RecordAuditPackageDelivery` | Records the exact accepted delivery of an audit package to its named recipient. |
| `RecordAuditPackageDelivery` | `ReportAuditPackageDelivery` | Names the person's action to report when and to whom the selected audit package was delivered. |
| `MaterializeAuditorComment` | `RecordAuditorComment` | Records the exact auditor comment admitted while its request was open. |
| `MaterializeAuditorResponse` | `CreateAuditorResponseDraft` | Creates the exact initial response draft admitted for an auditor request. |
| `AuthorizeAuditorResponseRequestResolutionUse` | `AuthorizeAuditorResponseRequestResolution` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleAuditorResponseRequestResolutionUse` | `CloseAuditorResponseRequestResolutionAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `MaterializeAuditorSampleSelection` | `RecordAuditorSampleSelection` | Records the frozen sample selected for the auditor request and population version. |
| `MaterializeClientIndependencePreview` | `RecordClientIndependenceEvaluation` | Records an evaluation made from exact frozen snapshots without granting production acceptance. |
| `AcquireEngagementServiceEpoch` | `ReserveEngagementServiceHistory` | Holds the exact service history stable while an engagement acceptance or assignment decision is made. |
| `ReleaseEngagementServiceEpoch` | `ReleaseEngagementServiceHistoryReservation` | Ends the exact temporary service-history hold after the decision or cancellation. |
| `AuthorizeClientServicePersonalDecisionUse` | `AuthorizeClientServicePersonalDecision` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleClientServicePersonalDecisionUse` | `CloseClientServicePersonalDecisionAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `MaterializeControlOccurrence` | `OpenDueControlOccurrence` | Opens the due control work item for its exact period, plan, owner, and reviewer. |
| `MaterializeCorrectiveAction` | `CreateCorrectiveAction` | Creates accepted corrective work with its own lifecycle, even if the finding later closes. |
| `AuthorizeCorrectiveActionFindingClosureUse` | `AuthorizeCorrectiveActionFindingClosure` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleCorrectiveActionFindingClosureUse` | `CloseCorrectiveActionFindingClosureAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `ApplyApplicationImportEffect` | `ApplyApplicationImportChange` | Applies the exact frozen import change provisionally while the full batch remains unpublished. |
| `FenceApplicationImportTarget` | `BlockCanceledApplicationImportChanges` | Blocks delayed changes from a canceled import batch from reaching the application. |
| `MaterializeEngagementManagementAcknowledgement` | `RecordEngagementManagementAcknowledgement` | Records the manager's acknowledgement of the exact engagement revision and service history. |
| `SettleArtifactDownloadAvailabilityUse` | `CloseArtifactDownloadAvailabilityAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeArtifactHandlingDownloadUse` | `AuthorizeArtifactHandlingDownload` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleArtifactHandlingDownloadUse` | `CloseArtifactHandlingDownloadAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeEvidenceRequestActionCompletionUse` | `AuthorizeEvidenceRequestActionCompletion` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleEvidenceRequestActionCompletionUse` | `CloseEvidenceRequestActionCompletionAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeFirmProfessionalDutyDesignationEngagementUse` | `AuthorizeFirmProfessionalDutyDesignationEngagement` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleFirmProfessionalDutyDesignationEngagementUse` | `CloseFirmProfessionalDutyDesignationEngagementAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeFirmStaffRecordEngagementUse` | `AuthorizeFirmStaffRecordEngagement` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleFirmStaffRecordEngagementUse` | `CloseFirmStaffRecordEngagementAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeFirmStaffProfessionalDutyUse` | `AuthorizeFirmStaffProfessionalDuty` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleFirmStaffProfessionalDutyUse` | `CloseFirmStaffProfessionalDutyAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `MaterializeIdentityRecoveryPreparation` | `PrepareAccountRecovery` | Prepares the exact recovery candidate only after both required identity proofs are verified. |
| `FreezeProgramScopeSnapshot` | `FinalizeProgramScopeSnapshot` | Finalizes the exact approved program scope as an immutable snapshot for later compliance work. |
| `AuthorizeIndependenceRuleCatalogEngagementUse` | `AuthorizeIndependenceRuleCatalogEngagement` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleIndependenceRuleCatalogEngagementUse` | `CloseIndependenceRuleCatalogEngagementAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `AuthorizeIndependenceRuleRatificationCatalogEngagementUse` | `AuthorizeIndependenceRuleRatificationCatalogEngagement` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleIndependenceRuleRatificationCatalogEngagementUse` | `CloseIndependenceRuleRatificationCatalogEngagementAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `ApplyNeutralMemberSourceMutation` | `ApplyMemberStatusChange` | Applies the admitted suspend, deprovision, or reinstate decision to the exact member episode. |
| `MaterializePartnerIndependenceDecision` | `RecordPartnerIndependenceEvaluation` | Records the partner evaluation against the exact engagement revision and service history. |
| `RecordPartnerIndependenceEvaluation` | `SubmitPartnerIndependenceEvaluation` | Names the partner's action to submit an independence evaluation with its exact engagement, service history, and rule version. |
| `CorrelatePersonMembership` | `SetPersonTenantMemberLink` | Links a workforce person to one tenant member or clears the link without changing access. |
| `SeedPlatformOperatorRoster` | `CreatePlatformOperatorRoster` | Creates the initial operator roster while preserving current-operator and last-operator protections. |
| `AuthorizePolicyCampaignParticipantCampaignClosureUse` | `AuthorizePolicyCampaignParticipantCampaignClosure` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettlePolicyCampaignParticipantCampaignClosureUse` | `ClosePolicyCampaignParticipantCampaignClosureAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `FreezePopulationSnapshot` | `FinalizePopulationSnapshot` | Finalizes the complete ordered population as the immutable snapshot used by later compliance work. |
| `SettlePracticeIndependenceReservation` | `ClosePracticeIndependenceReservation` | Ends the temporary practice reservation after commitment or cancellation. |
| `ClaimProgramRolloverExecution` | `ReserveProgramRolloverForExecution` | Reserves the exact approved rollover for one run so its approval cannot change while the successor period is prepared. |
| `AuthorizeProviderAssuranceReportProviderReviewUse` | `AuthorizeProviderAssuranceReportProviderReview` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleProviderAssuranceReportProviderReviewUse` | `CloseProviderAssuranceReportProviderReviewAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `MaterializeProviderRiskAcceptanceLink` | `LinkProviderRiskAcceptanceToGap` | Links the exact active risk acceptance to the provider gap that admitted it. |
| `AuthorizeReadinessGapPlanReadinessDecisionUse` | `AuthorizeReadinessGapPlanReadinessDecision` | The command authorizes the named business action; the extra Use suffix describes the coordination mechanism. |
| `SettleReadinessGapPlanReadinessDecisionUse` | `CloseReadinessGapPlanReadinessDecisionAuthorization` | The command ends the exact authorization after its business action or cancellation. |
| `SettleEngagementPersonalDecision` | `CloseEngagementPersonalDecision` | Closes the exact engagement personal decision after its authorization or cancellation. |
| `ApplyNeutralAdministratorAssignmentMutation` | `RemoveAdministratorAssignment` | Removes an initial administrator assignment only when the member has no current contribution. |
| `CleanupRetiredMembershipRelationship` | `RemoveRetiredMemberAccessAndAssignments` | Removes team assignments and access grants after the exact membership episode ends. |
| `ConfigureTenantIdentityProvider` | `ConfigureTenantSignInConnection` | Sets up the tenant's external sign-in connection and its approved credentials. |
| `DisableTenantIdentityProvider` | `DisableTenantSignInConnection` | Turns off external sign-in for the tenant while retaining the recorded connection. |
| `RecordTenantIdentityProviderTestResult` | `RecordSignInConnectionTestResult` | Records whether the tenant's current sign-in connection passed; changed credentials invalidate earlier results. |
| `RotateTenantIdentityProviderCredentials` | `RotateTenantSignInCredentials` | Replaces the tenant's sign-in credentials and invalidates earlier connection tests. |
| `TestTenantIdentityProvider` | `TestTenantSignInConnection` | Tests whether the tenant's configured external sign-in connection works. |
| `SettleTypeIIPeriodRange` | `CloseTypeIIPeriodRangeReservation` | Ends the exact Type II date reservation after acceptance, replacement, or cancellation. |
| `GenerateDueControlOccurrences` | `OpenDueControlOccurrences` | Opens the control work due under the exact accepted plan and period. |
| `ContinueWithOidcProvider` | `ContinueSignInWithExternalProvider` | Completes sign-in with an identity verified by an external sign-in provider. |
| `ContinueWithDeveloperIdentity` | `SignInWithDevelopmentAccount` | Signs in with the deterministic development identity when developer sign-in is enabled. |
| `LinkOidcProviderIdentity` | `LinkExternalSignInToCurrentAccount` | Links the verified external sign-in identity to the account already signed in in this browser. |
| `SettleRecoveryIdentityProtection` | `EndRecoveryIdentityProtection` | Ends protection of the exact recovery identity slot, including when protection never began. |

## Events

| Selected source name | Target event name | Why it names the completed fact |
| --- | --- | --- |
| `AccessReviewItemCampaignCompletionUseAuthorized` | `AccessReviewItemCampaignCompletionAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `AccessReviewItemCampaignCompletionUseSettled` | `AccessReviewItemCampaignCompletionAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `AccessReviewItemDecisionSettled` | `AccessReviewItemDecisionAuthorizationClosed` | Names the end of the exact authorization after publication or cancellation; the payload preserves which outcome occurred. |
| `AdvisorArtifactDownloadUseAuthorized` | `AdvisorArtifactDownloadAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `AdvisorArtifactDownloadUseSettled` | `AdvisorArtifactDownloadAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `AdvisorHandoffReadiedForReview` | `AdvisorHandoffOpenedForReview` | Names the completed opening of the handoff to its assigned advisor for review. |
| `ApplicationImportEffectCanceled` | `ApplicationImportChangesBlockedAfterCancellation` | Names the protection that prevents a canceled import batch from changing this application after cancellation. |
| `ApplicationImportEffectPending` | `ApplicationImportChangeProvisionallyApplied` | Names the exact change applied provisionally while the import batch awaits its final result. |
| `ApplicationImportPlanSealed` | `ApplicationImportPlanAccepted` | Names acceptance of the complete frozen import plan that starts import execution. |
| `ApplicationImportRetirementProposalSealed` | `ApplicationImportRetirementProposalFinalized` | Names the complete frozen omission proposal and does not imply authority to retire an application. |
| `ApplicationImportRowCorrelated` | `ApplicationImportRowChoiceRecorded` | Names the person's recorded link-existing or create-new choice for one staged row. |
| `ApplicationImportSourceSettled` | `ApplicationImportSourceBatchCompleted` | Names the terminal result of source-claim processing; published source claims remain active after the batch completes. |
| `ApplicationImportStaged` | `ApplicationImportPrepared` | Names the immutable source observation and complete rows submitted for an import batch. |
| `ApplicationImportTargetSettled` | `ApplicationImportTargetHoldEnded` | Names the end of the exact application hold; the payload preserves whether publication, cancellation, or failure ended it. |
| `ArtifactDownloadAvailabilityUseSettled` | `ArtifactDownloadAvailabilityAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `ArtifactHandlingDownloadUseAuthorized` | `ArtifactHandlingDownloadAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `ArtifactHandlingDownloadUseSettled` | `ArtifactHandlingDownloadAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `ArtifactRetentionBound` | `ArtifactRetentionPeriodSet` | Names the historical retention period assigned to an artifact source. |
| `AuditorResponseRequestResolutionUseAuthorized` | `AuditorResponseRequestResolutionAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `AuditorResponseRequestResolutionUseSettled` | `AuditorResponseRequestResolutionAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `ClientServicePersonalDecisionUseAuthorized` | `ClientServicePersonalDecisionAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `ClientServicePersonalDecisionUseSettled` | `ClientServicePersonalDecisionAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `CorrectiveActionAddedV2` | `CorrectiveActionAdded` | Names the creation of the admitted corrective action without exposing the implementation version suffix. |
| `CorrectiveActionCompletedV2` | `CorrectiveActionCompleted` | Names the completed corrective action without exposing the implementation version suffix. |
| `CorrectiveActionFindingClosureUseAuthorized` | `CorrectiveActionFindingClosureAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `CorrectiveActionFindingClosureUseSettled` | `CorrectiveActionFindingClosureAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `EngagementPersonalDecisionMaterialized` | `EngagementPersonalDecisionRecorded` | Names the recorded personal decision; releasing its content pin does not advance the engagement revision. |
| `EngagementServiceEpochAuthorized` | `EngagementServiceHistoryReserved` | Names the exact service-history hold while the engagement decision is made. |
| `EngagementServiceEpochSettled` | `EngagementServiceHistoryReservationEnded` | Names the end of the temporary service-history hold after decision or cancellation. |
| `EvidenceRedactionApprovedV2` | `EvidenceRedactionApproved` | Names the personal approval of the exact evidence redaction without exposing the implementation version suffix. |
| `EvidenceRedactionPreparedV2` | `EvidenceRedactionPrepared` | Names the prepared evidence redaction and its exact source without exposing the implementation version suffix. |
| `EvidenceRequestActionCompletionUseAuthorized` | `EvidenceRequestActionCompletionAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `EvidenceRequestActionCompletionUseSettled` | `EvidenceRequestActionCompletionAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `FindingAcceptanceLinkedV2` | `FindingAcceptanceLinked` | Names the completed link between the finding and its risk acceptance or waiver without exposing the implementation version suffix. |
| `FindingClosedV2` | `FindingClosed` | Names the completed closure of the finding without exposing the implementation version suffix. |
| `FindingRaisedV2` | `FindingRaised` | Names the creation of the finding without exposing the implementation version suffix. |
| `FindingReopenedV2` | `FindingReopened` | Names the reopening of the finding without exposing the implementation version suffix. |
| `FindingRevisedV2` | `FindingRevised` | Names the completed revision of the finding without exposing the implementation version suffix. |
| `FirmProfessionalDutyDesignationEngagementUseAuthorized` | `FirmProfessionalDutyDesignationEngagementAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `FirmProfessionalDutyDesignationEngagementUseSettled` | `FirmProfessionalDutyDesignationEngagementAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `FirmStaffProfessionalDutyUseAuthorized` | `FirmStaffProfessionalDutyAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `FirmStaffProfessionalDutyUseSettled` | `FirmStaffProfessionalDutyAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `FirmStaffRecordEngagementUseAuthorized` | `FirmStaffRecordEngagementAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `FirmStaffRecordEngagementUseSettled` | `FirmStaffRecordEngagementAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `IndependenceRuleCatalogEngagementUseAuthorized` | `IndependenceRuleCatalogEngagementAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `IndependenceRuleCatalogEngagementUseSettled` | `IndependenceRuleCatalogEngagementAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `IndependenceRuleRatificationCatalogEngagementUseAuthorized` | `IndependenceRuleRatificationCatalogEngagementAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `IndependenceRuleRatificationCatalogEngagementUseSettled` | `IndependenceRuleRatificationCatalogEngagementAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `MemberDeprovisionCleanupCompleted` | `MemberAccessAndAssignmentsRemoved` | Names the completed removal of direct access and team assignments for the exact deprovisioned membership episode. |
| `PersonMembershipCorrelated` | `PersonMemberLinkChanged` | Names either linking a workforce person to a tenant member or clearing that link; it does not grant access. |
| `PlatformOperatorRosterSeeded` | `PlatformOperatorRosterCreated` | Names the completed creation of the roster's initial set of operator users. |
| `PolicyCampaignParticipantCampaignClosureUseAuthorized` | `PolicyCampaignParticipantCampaignClosureAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `PolicyCampaignParticipantCampaignClosureUseSettled` | `PolicyCampaignParticipantCampaignClosureAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `PopulationSnapshotChunkStored` | `PopulationSnapshotPartRecorded` | Names one ordered part of population data recorded before the complete snapshot is finalized. |
| `PopulationSnapshotFrozen` | `PopulationSnapshotFinalized` | Names the complete, immutable population snapshot that is ready for later compliance work. |
| `PracticeIndependenceReservationSettled` | `PracticeIndependenceReservationEnded` | Names the end of the temporary reservation after commitment or cancellation. |
| `ProgramRolloverExecutionClaimed` | `ProgramRolloverReservedForExecution` | Names the reservation of the exact approved rollover for one run without exposing the internal claim mechanism. |
| `ProviderAssuranceReportProviderReviewUseAuthorized` | `ProviderAssuranceReportProviderReviewAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `ProviderAssuranceReportProviderReviewUseSettled` | `ProviderAssuranceReportProviderReviewAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `ReadinessGapPlanReadinessDecisionUseAuthorized` | `ReadinessGapPlanReadinessDecisionAuthorized` | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| `ReadinessGapPlanReadinessDecisionUseSettled` | `ReadinessGapPlanReadinessDecisionAuthorizationClosed` | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| `RecoveryIdentityProtectionSettled` | `RecoveryIdentityProtectionEnded` | Names the end of protection for the exact recovery identity slot, including when protection never began. |
| `SnapshotFrozen` | `SnapshotFinalized` | Names the complete, immutable snapshot after it can no longer change. |
| `SystemDescriptionBound` | `SystemDescriptionLinkedToExamination` | Names which approved system description version supports the exact examination snapshot. |
| `SystemDescriptionSourcesBound` | `SystemDescriptionSourcesRecorded` | Names the current sources a member reconciled against a system description draft. |
| `TenantActivatedV2` | `TenantActivated` | Names the tenant's completed activation without exposing the implementation version suffix. |
| `TenantManagerSourceMutationAdmitted` | `TenantManagerMemberChangeAccepted` | Names the accepted member change that is serialized with other changes to the same membership episode. |
| `TenantOffboardingStartedV2` | `TenantOffboardingStarted` | Names the start of tenant offboarding without exposing the implementation version suffix. |
| `TenantReactivatedV2` | `TenantReactivated` | Names the tenant's completed reactivation without exposing the implementation version suffix. |
| `TenantSuspendedV2` | `TenantSuspended` | Names the tenant's completed suspension without exposing the implementation version suffix. |
| `TypeIIPeriodRangeAuthorized` | `TypeIIPeriodDateRangeReserved` | Names the reserved observation dates before a Type II period opening or amendment. |
| `TypeIIPeriodRangeSettled` | `TypeIIPeriodDateRangeReservationEnded` | Names the end of the exact date reservation after acceptance, replacement, or cancellation. |
| `WorkDigestDispatchAttemptStarted` | `WorkDigestDeliveryAttemptStarted` | Names the start of one scheduled digest delivery attempt without using the transport term dispatch. |
| `WorkDigestDispatchOutcomeRecorded` | `WorkDigestDeliveryOutcomeRecorded` | Names the recorded result of one work digest delivery attempt. |
| `WorkDigestDispatchScheduled` | `WorkDigestDeliveryScheduled` | Names the scheduled work digest delivery time. |
| `WorkDigestUnknownRetryAuthorized` | `WorkDigestRetryApprovedAfterUnknownDelivery` | Names the operator's approval to retry a weekly digest when its prior delivery result could not be confirmed. |


### Meaning clarified from the choreography

| Target event name | Meaning |
| --- | --- |
| `ProgramRolloverReservedForExecution` | The exact independently approved rollover version and content were reserved for one run. The reservation prevents revision or rejection while the successor period is prepared. |

Event discriminator strings and versions remain exactly as recorded in the source contracts. The target type name is a separate decision; implementation must preserve wire compatibility while adopting the target name.

## Reactors

| Selected root name | Target reactor name | Business reason |
| --- | --- | --- |
| `ProfessionalDutyChangeReactor` | `ApplyOrRevokeProfessionalDutyDesignationReactor` | Apply or revoke an exact professional duty designation while preserving its unique identity and engagement decisions. |
| `AccessReviewLaunchFanOutReactor` | `OpenAccessReviewWorkReactor` | Give reviewers a task for every in-scope account so each account receives an explicit access decision. |
| `AccessReviewBulkAdmissionReactor` | `PublishApprovedAccessReviewDecisionReactor` | Make all decisions from one bulk review visible together so no access change takes effect from a partial review. |
| `AccessReviewBulkCancellationReactor` | `CancelUnpublishedAccessReviewDecisionsReactor` | Cancel delayed item decisions before their reservations are released; published decisions recover forward. |
| `AdvisorFeedbackMaterializationReactor` | `PreserveAdvisorFeedbackAfterHandoffReactor` | Keep feedback already accepted into its thread even if the advisor handoff later ends. |
| `ImportSourceAdmissionReactor` | `ApplyAcceptedApplicationImportReactor` | Prevent two imports from consuming the same source updates and publish changes only when the accepted import can be applied as a whole. |
| `ImportCancellationReactor` | `CancelApplicationImportReactor` | Cancel the exact import batch, stop delayed application changes, and release only its own source claims and holds. |
| `ProviderChangeAcceptedReactor` | `KeepProviderNamesUniqueReactor` | Keep provider names unique through accepted provider changes and retirement. |
| `LocationChangeAcceptedReactor` | `KeepLocationNamesUniqueReactor` | Keep location names unique while an accepted location change claims the new name and releases the old one. |
| `OperationalProcessChangeAcceptedReactor` | `KeepOperationalProcessNamesUniqueReactor` | Keep active operational process names unique through accepted revisions and retirement. |
| `AuditPackageBuildProcessReactor` | `BuildAuditPackageForReviewReactor` | Build the approved audit package as a verified archive that can be delivered for review. |
| `AuditorResponseMaterializationReactor` | `PreserveAuditorResponseDraftAfterRequestReactor` | Keep an accepted auditor response draft even if the request later closes. |
| `AuditorCommentMaterializationReactor` | `PreserveAuditorCommentsAfterRequestReactor` | Keep comments accepted while an auditor request was open even if the request later closes. |
| `CorrectiveActionMaterializationReactor` | `OpenCorrectiveActionForFindingReactor` | Start accepted corrective work as an independent lifecycle even if the finding later closes. |
| `ReadinessRunReactor` | `ReassessProgramReadinessReactor` | Update current program readiness from the exact frozen assessment. |
| `ControlOccurrenceFanOutReactor` | `OpenDueControlWorkReactor` | Open control work due in the accepted period for its exact plan, owners, and reviewers. |
| `IdentityRecoveryAcceptanceReactor` | `CompleteAccountRecoveryReactor` | Complete verified account recovery, register the recovered identity, revoke the old identity when required, and send notice. |
| `AdministratorNeutralMemberMutationReactor` | `ApplyAcceptedMemberStatusChangeReactor` | Apply an admitted suspension, deprovisioning, or reinstatement to the exact member episode. |
| `AdministratorNeutralAssignmentMutationReactor` | `RemoveUnneededAdministratorAssignmentReactor` | Remove an initial administrator assignment only when the member never became active or no longer contributes. |
| `TypeIIRangeReactor` | `ReserveAcceptedTypeIIPeriodDatesReactor` | Reserve non-overlapping observation dates and apply only the exact accepted range. |
| `TypeIIRangeReleaseReactor` | `ReleaseTypeIIPeriodDateReservationReactor` | Release the exact old or closed observation dates after a period ends or its change is canceled. |
| `AccessReviewCompletionSettlementReactor` | `ApplyAccessReviewCampaignOutcomeToItemsReactor` | Keep each access review item consistent with the campaign's final outcome so completed or canceled work cannot continue on its own. |
| `ReadinessDecisionReactor` | `RecordProgramReadinessAndTypeIDecisionsReactor` | Record the exact readiness and Type I decisions for the selected program and gap plan. |
| `ProviderReviewReactor` | `RecordProviderAssuranceReportReviewReactor` | Record the review of the exact assurance report and end its review authorization. |
| `PolicyParticipantResultReactor` | `RecordPolicyRequirementOutcomesReactor` | Record the participant's exact acknowledgement, training completion, or approved waiver. |
| `AccessReviewBulkCancellationCompletionReactor` | `CompleteAccessReviewBatchCancellationReactor` | Keep canceled bulk decisions from changing access later by finishing cancellation only after every reviewed item is protected. |
| `AccessReviewBulkSettlementReactor` | `ApplyPublishedAccessReviewDecisionReactor` | Apply the published batch decision to each selected item while canceled decisions stay hidden. |
| `AuditPackageDeliveryAdmissionReactor` | `RecordAuditPackageDeliveryReactor` | Record delivery of the exact approved package to its named recipient. |
| `ProviderRiskAcceptanceLinkReactor` | `KeepRiskAcceptanceWithProviderGapReactor` | Keep the exact accepted risk linked to the provider gap that admitted it, even if the gap later closes. |
| `EngagementPersonalDecisionReactor` | `RecordEngagementIndependenceDecisionsReactor` | Record each person's independence decision against the exact engagement revision and service history. |
| `one committed EvidenceArtifactDownloadRequested starter` | `FulfillAuthorizedArtifactDownloadRequestReactor` | Let people download evidence only when the artifact is available, its handling rules allow the download, and consultant access has the required advisor approval. |
| `AuditorSampleSelectionMaterializationReactor` | `RecordAuditorSampleSelectionReactor` | Record the exact complete sample admitted for an open auditor request. |
| `ClientIndependencePreviewReactor` | `EvaluateClientIndependenceForAcceptanceReactor` | Give the service-acceptance decision a complete independence result so impaired or unresolved relationships block acceptance. |
| `EmailChallengeDeliveryReactor` | `SendAccountVerificationAndRecoveryEmailsReactor` | Send the messages people need to verify an email address or recover an account. |
| `TenantInvitationDeliveryProcessReactor` | `InviteMemberToJoinTenantReactor` | Send the invitation that lets an invited member join the tenant. |
| `EvidenceArtifactInspectionEffectsReactor` | `ApplyArtifactInspectionOutcomeReactor` | Protect users from unsafe evidence by applying the inspector's decision to the registered artifact before it can be used or downloaded. |
| `MemberRetirementReactor` | `RemoveMemberAssignmentsAndAccessAfterMembershipEndsReactor` | Remove team assignments and access grants after the exact membership episode ends. |
| `RetiredMemberGrantReactor` | `RevokeAccessAfterMemberRetirementReactor` | Keep tenant access tied to a live membership: revoke any access grant made for a membership episode after that episode ends. |
| `RetiredTeamAssignmentReactor` | `RemoveTeamAssignmentAfterMemberRetirementReactor` | Prevent someone whose membership has ended from remaining assigned to a team when a delayed assignment arrives. |
| `RoleCleanupReactor` | `RemoveRoleAssignmentsAfterRoleDeletionReactor` | When a custom role is deleted, remove its permission and team assignments so obsolete role links cannot continue granting access. |
| `TeamCleanupReactor` | `RemoveTeamAssignmentsAfterTeamDeletionReactor` | Make sure deleting a team also ends each person’s membership in it. |
| `TenantRbacBootstrapReactor` | `SetUpStartingTenantAccessReactor` | Give a new tenant its starting roles, teams, permissions, and owner access. |
| `FirmStaffStatusReevaluationReactor` | `ReassessFirmStaffIndependenceAfterChangesReactor` | Keep client-service independence decisions current when a firm's staff status changes, and block affected acceptances that relied on outdated staff information. |
| `AcceptedStaffDirectoryReconciliationReactor` | `ReassessStaffIndependenceAfterChangesReactor` | Prevent a client service from relying on an independence decision after a staff member's status or assignment has changed. |
| `LinkImpactReviewWorkReactor` | `RequestImpactReviewAfterSignificantChangeReactor` | Make sure a significant change's required control and risk reviews are assigned and linked before the change is treated as addressed. |
| `SystemDescriptionDecisionReactor` | `UpdateSystemDescriptionAfterSignificantChangeReactor` | Require an approved system description or a recorded no-change decision before closing the impact review for a significant change or incident. |
| `TenantSlugReactor` | `KeepTenantWebAddressesUniqueReactor` | Confirm, reject, or release a tenant web address while preserving its unique claim. |
| `TenantInvitationReactor` | `ActivateMembershipAfterInvitationAcceptanceReactor` | Create the accepted member's membership, team access, guest grant when needed, and first-admin tenant activation. |
| `TenantRegistrationReactor` | `EstablishTenantAndInviteOwnerReactor` | Give each new organization a usable, unique web address and invite its first administrator when that person is not already a member. |
| `EmailReservationReactor` | `KeepTenantEmailAddressesUniqueReactor` | Reserve the normalized email address so two tenant identities cannot claim the same address. |
| `one new committed execution-claim starter` | `StartApprovedProgramRolloverReactor` | Start successor-period work only after the exact approved rollover has been reserved for one execution. |
| `ApplicationNameChangeReactor` | `KeepApplicationNamesUniqueReactor` | Keep every application name and alias unique while names are changed or an application is retired. |
| `ApplicationRetirementReactor` | `RemoveRelationshipsAfterApplicationRetirementReactor` | Remove active application relationships when the exact application is retired or merged. |
| `ArtifactLegalHoldReactor` | `ProtectArtifactsUnderLegalHoldReactor` | Stop an approved artifact disposal while a legal hold applies and allow it to proceed only after all holds are clear. |
| `ClientExportDeliveryReactor` | `KeepEngagementEvidenceProtectedAfterDeliveryReactor` | Record package delivery on the engagement hold so evidence remains protected for the required period. |
| `RiskEvaluationReactor` | `RequestRiskReassessmentWhenRiskChangesReactor` | Request a new assessment when a risk acceptance expires or recorded exposure exceeds appetite. |

## Implementation boundary

These target names change the proposed model only. Selected source names, routes, command identities, event discriminator strings, and discriminator versions remain compatibility evidence. This page makes no claim that the renamed contracts are already implemented.
