using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Publishes an independently reviewed, immutable template version snapshot.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-version-published", 2)]
public sealed record MethodologyTemplateVersionPublished(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateView State,
    DateTimeOffset OccurredAt) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance? Operation { get; init; }
}
