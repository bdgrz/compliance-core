using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Records an owning-workflow record creation receipt that remains a draft.</summary>
[Discriminator("bdgrz.methodologytemplates.template-application-draft-created", 1)]
public sealed record TemplateApplicationDraftCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TemplateApplicationView State,
    DateTimeOffset OccurredAt) : DomainEvent;
