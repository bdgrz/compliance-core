using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.accepted", 1)]
public sealed record RiskAccepted(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskAcceptanceView Acceptance) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OriginalExpectedRevision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoredIntentSha256 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SeparationOfDutiesWaiverProof? WaiverProof { get; init; }
}
