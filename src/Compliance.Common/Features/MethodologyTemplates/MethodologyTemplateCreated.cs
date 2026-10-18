using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Creates the first replayable version snapshot of a platform-owned template.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-created", 2)]
public sealed record MethodologyTemplateCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    MethodologyTemplateView State,
    DateTimeOffset OccurredAt) : DomainEvent;
