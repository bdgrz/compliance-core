using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Complete compliance incident chronology, impact decision, and resolution history.</summary>
public sealed record ComplianceIncidentView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ComplianceIncidentId,
    string Status,
    long Revision,
    int CurrentVersionNumber,
    ComplianceIncidentVersionView? Current,
    IReadOnlyList<ComplianceIncidentVersionView> Versions,
    IReadOnlyList<ComplianceIncidentImpactAssessmentView> Assessments,
    IReadOnlyList<ComplianceIncidentSystemDescriptionDecisionView> SystemDescriptionDecisions,
    ComplianceIncidentResolutionView? Resolution,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public ComplianceIncidentImpactAssessmentView? CurrentAssessment => Assessments.Count > 0 &&
        Assessments[^1] is { } assessment && assessment.ContentVersion == CurrentVersionNumber
            ? assessment : null;

    [JsonIgnore]
    public ComplianceIncidentSystemDescriptionDecisionView? CurrentSystemDescriptionDecision =>
        SystemDescriptionDecisions.Count > 0 && SystemDescriptionDecisions[^1] is { } decision &&
        decision.ContentVersion == CurrentVersionNumber ? decision : null;

    [JsonIgnore]
    public bool PeriodCloseEligible => Status == "resolved" && Resolution is not null &&
        (CurrentAssessment?.RequiresSystemDescriptionReview != true || CurrentSystemDescriptionDecision is not null);
}
