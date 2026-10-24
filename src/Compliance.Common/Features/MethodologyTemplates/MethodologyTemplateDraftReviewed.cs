using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Retains an independent review decision for the current template version.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-draft-reviewed", 1)]
public sealed record MethodologyTemplateDraftReviewed(
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
