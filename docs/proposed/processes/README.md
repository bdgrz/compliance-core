# Process definitions

Selected process definitions: **30**.

| Process | Root source events | Ordered |
| --- | --- | --- |
| [AccessReviewLaunchFanOut](access-review-launch-fan-out.md) | AccessReviewCampaignLaunched | false |
| [BulkAccessDecisionProcess](bulk-access-decision-process.md) | AccessReviewDecisionBatchAdmitted | true |
| [AccessReviewCampaignCompletionProcess](access-review-campaign-completion-process.md) | AccessReviewCampaignCompletionAccepted | true |
| [FindingClosureProcess](finding-closure-process.md) | FindingClosureAccepted | true |
| [ReadinessDecisionProcess](readiness-decision-process.md) | ReadinessDecisionAccepted, TypeIEntryDecisionAccepted | true |
| [PolicyCampaignClosureProcess](policy-campaign-closure-process.md) | PolicyCampaignClosureAccepted | true |
| [ProviderReviewProcess](provider-review-process.md) | ProviderReviewRequested | true |
| [RiskActionCompletionReviewProcess](risk-action-completion-review-process.md) | RiskTreatmentActionCompletionReviewAccepted | true |
| [ProviderNameChangeProcess](provider-name-change-process.md) | ProviderChangeAccepted | true |
| [ReadinessRunProcess](readiness-run-process.md) | ReadinessAssessmentRequested | true |
| [ArtifactDownloadProcess](artifact-download-process.md) | EvidenceArtifactDownloadRequested | true |
| [ControlOccurrenceFanOut](control-occurrence-fan-out.md) | DueControlOccurrencesGenerated | false |
| [IndependentChildMaterialization](independent-child-materialization.md) | AdvisorFeedbackAdmitted, AuditorResponseAdmitted, AuditorCommentAdmitted, CorrectiveActionAdmitted, AuditorSampleSelectionAdmitted, ProviderCoverageGapRiskAcceptanceAdmitted, PolicyParticipantResultAdmitted | false |

