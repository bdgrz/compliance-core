using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Remediation.Events;
namespace Bdgrz.Compliance.Features.SourceUse.History;

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
