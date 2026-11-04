using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The owning context answered one applied entry with its immutable terminal outcome.</summary>
[Discriminator("bdgrz.methodology-templates.template-entry-outcome-recorded", 1)]
public sealed record TemplateEntryOutcomeRecorded(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationEventId, TemplateEntryOutcome Outcome) : DomainEvent;
