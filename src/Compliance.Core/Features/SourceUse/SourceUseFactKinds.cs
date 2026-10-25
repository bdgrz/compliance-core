using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>The existing reducer registrations, expressed once for full transient stream validation.</summary>
static class SourceUseFactKinds
{
    public static (SourceUsePurpose Purpose, bool Granted)? Classify(DomainEvent fact) => fact switch
    {
        AccessReviewItemCampaignCompletionUseAuthorized => (SourceUsePurpose.CampaignCompletion, true),
        AccessReviewItemCampaignCompletionUseSettled => (SourceUsePurpose.CampaignCompletion, false),
        AccessReviewItemDecisionAuthorized => (SourceUsePurpose.BulkDecision, true),
        AccessReviewItemDecisionSettled => (SourceUsePurpose.BulkDecision, false),
        AuditorResponseRequestResolutionUseAuthorized => (SourceUsePurpose.RequestResolution, true),
        AuditorResponseRequestResolutionUseSettled => (SourceUsePurpose.RequestResolution, false),
        ClientServicePersonalDecisionUseAuthorized => (SourceUsePurpose.EngagementPersonalDecision, true),
        ClientServicePersonalDecisionUseSettled => (SourceUsePurpose.EngagementPersonalDecision, false),
        CorrectiveActionFindingClosureUseAuthorized => (SourceUsePurpose.FindingClosure, true),
        CorrectiveActionFindingClosureUseSettled => (SourceUsePurpose.FindingClosure, false),
        EngagementServiceEpochAuthorized => (SourceUsePurpose.EngagementAcceptance, true),
        EngagementServiceEpochSettled => (SourceUsePurpose.EngagementAcceptance, false),
        EvidenceRequestActionCompletionUseAuthorized => (SourceUsePurpose.ActionCompletionReview, true),
        EvidenceRequestActionCompletionUseSettled => (SourceUsePurpose.ActionCompletionReview, false),
        FirmProfessionalDutyDesignationEngagementUseAuthorized => (SourceUsePurpose.EngagementPersonalDecision, true),
        FirmProfessionalDutyDesignationEngagementUseSettled => (SourceUsePurpose.EngagementPersonalDecision, false),
        FirmStaffProfessionalDutyUseAuthorized => (SourceUsePurpose.ProfessionalDutyRecording, true),
        FirmStaffProfessionalDutyUseSettled => (SourceUsePurpose.ProfessionalDutyRecording, false),
        FirmStaffRecordEngagementUseAuthorized => (SourceUsePurpose.EngagementPersonalDecision, true),
        FirmStaffRecordEngagementUseSettled => (SourceUsePurpose.EngagementPersonalDecision, false),
        IndependenceRuleCatalogEngagementUseAuthorized => (SourceUsePurpose.EngagementPersonalDecision, true),
        IndependenceRuleCatalogEngagementUseSettled => (SourceUsePurpose.EngagementPersonalDecision, false),
        IndependenceRuleRatificationCatalogEngagementUseAuthorized => (SourceUsePurpose.EngagementPersonalDecision, true),
        IndependenceRuleRatificationCatalogEngagementUseSettled => (SourceUsePurpose.EngagementPersonalDecision, false),
        PolicyCampaignParticipantCampaignClosureUseAuthorized => (SourceUsePurpose.CampaignClosure, true),
        PolicyCampaignParticipantCampaignClosureUseSettled => (SourceUsePurpose.CampaignClosure, false),
        PracticeIndependenceReservationSettled => (SourceUsePurpose.ActualStaffAssignment, false),
        PracticeIndependenceReserved => (SourceUsePurpose.ActualStaffAssignment, true),
        ProviderAssuranceReportProviderReviewUseAuthorized => (SourceUsePurpose.ProviderReview, true),
        ProviderAssuranceReportProviderReviewUseSettled => (SourceUsePurpose.ProviderReview, false),
        ReadinessGapPlanReadinessDecisionUseAuthorized => (SourceUsePurpose.ReadinessDecision, true),
        ReadinessGapPlanReadinessDecisionUseSettled => (SourceUsePurpose.ReadinessDecision, false),
        _ => null,
    };
}
