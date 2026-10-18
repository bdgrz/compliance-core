using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Pins one published template version and creates tenant-owned draft intents.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-applied", 2)]
public sealed record MethodologyTemplateApplied(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TemplateApplicationView State,
    DateTimeOffset OccurredAt) : DomainEvent;
