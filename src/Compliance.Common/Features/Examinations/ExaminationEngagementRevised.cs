using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records a complete successor revision of an unfrozen engagement.</summary>
[Discriminator("bdgrz.examinations.examination-engagement-revised", 1)]
public sealed record ExaminationEngagementRevised(
    Uuid TenantId,
    ExaminationEngagementView Engagement,
    DateTimeOffset OccurredAt) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance? Operation { get; init; }
}
