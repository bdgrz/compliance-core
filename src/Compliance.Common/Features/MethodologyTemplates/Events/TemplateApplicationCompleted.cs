using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The current applied version finished all destination outcomes, allowing a later version.</summary>
[Discriminator("bdgrz.methodology-templates.template-application-completed", 1)]
public sealed record TemplateApplicationCompleted(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationCommandId, TemplateApplicationOutcomeCounts Counts,
    Uuid CompletionSourceEventId) : DomainEvent;
