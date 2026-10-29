using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskAssessmentRecordedBody(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, RiskAssessmentView Assessment)
{
    public long? OriginalExpectedRevision { get; init; }
    public string? AuthoredIntentSha256 { get; init; }
    public RiskMethodVersionView? Method { get; init; }
    public bool HasAcceptedControlTreatment { get; init; }
}
