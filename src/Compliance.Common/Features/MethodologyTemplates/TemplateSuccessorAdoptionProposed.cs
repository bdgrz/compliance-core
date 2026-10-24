using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Proposes one exact approved successor to a tenant application.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-adoption-proposed", 2)]
public sealed record TemplateSuccessorAdoptionProposed(
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
