using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Complete compliance-facing change history and current period-close eligibility.</summary>
public sealed record SignificantChangeView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SignificantChangeId,
    string Status,
    long Revision,
    int CurrentVersionNumber,
    SignificantChangeVersionView? Current,
    IReadOnlyList<SignificantChangeVersionView> Versions,
    IReadOnlyList<SignificantChangeImpactView> Assessments,
    IReadOnlyList<SignificantChangeWorkLinkView> ReviewWorkLinks,
    IReadOnlyList<SignificantChangeSystemDescriptionDecisionView> SystemDescriptionDecisions,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public SignificantChangeImpactView? CurrentAssessment => Assessments.Count > 0 &&
        Assessments[^1] is { } assessment && assessment.ContentVersion == CurrentVersionNumber
            ? assessment : null;

    [JsonIgnore]
    public SignificantChangeSystemDescriptionDecisionView? CurrentSystemDescriptionDecision =>
        SystemDescriptionDecisions.Count > 0 && SystemDescriptionDecisions[^1] is { } decision &&
        decision.ContentVersion == CurrentVersionNumber ? decision : null;

    [JsonIgnore]
    public bool PeriodCloseEligible => CurrentAssessment is { } assessment &&
        assessment.RelatedReviewWorkIds.All(id => ReviewWorkLinks.Any(link =>
            link.ContentVersion == CurrentVersionNumber && link.WorkId == id)) &&
        (!assessment.RequiresSystemDescriptionReview || CurrentSystemDescriptionDecision is not null);
}
