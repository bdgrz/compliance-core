using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Every listed entry has a terminal outcome, with exact totals for its applied version.</summary>
[Discriminator("bdgrz.methodology-templates.template-version-application-completed", 1)]
public sealed record TemplateVersionApplicationCompleted(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationEventId, TemplateApplicationOutcomeCounts Counts, Uuid ApplicationCommandId = default) : DomainEvent;
