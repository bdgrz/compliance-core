using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Retains a new authored template version snapshot without changing published versions.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-draft-revised", 2)]
public sealed record MethodologyTemplateDraftRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateView State,
    DateTimeOffset OccurredAt) : DomainEvent;
