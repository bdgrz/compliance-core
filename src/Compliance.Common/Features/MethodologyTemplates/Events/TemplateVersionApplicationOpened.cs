using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>One applied version's exact entry set was opened from its committed application source.</summary>
[Discriminator("bdgrz.methodology-templates.template-version-application-opened", 1)]
public sealed record TemplateVersionApplicationOpened(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationEventId, IReadOnlyList<string> EntryKeys) : DomainEvent;
