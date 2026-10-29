using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskTreatmentChosenBody(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, RiskTreatmentView Treatment)
{
    public long? OriginalExpectedRevision { get; init; }
    public string? AuthoredIntentSha256 { get; init; }
    public Uuid? CommandId { get; init; }
}
