using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records a tenant's attributable successor decision and rationale.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-disposition-recorded", 2)]
public sealed record TemplateSuccessorDispositionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TemplateApplicationView State,
    DateTimeOffset OccurredAt) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance? Operation { get; init; }
}
