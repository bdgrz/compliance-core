using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment.chosen", 1)]
public sealed record RiskTreatmentChosen(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskTreatmentView Treatment) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? CommandId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OriginalExpectedRevision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoredIntentSha256 { get; init; }
}
