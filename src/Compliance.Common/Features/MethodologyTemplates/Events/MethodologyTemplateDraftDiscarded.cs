using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>An active separate draft was discarded with its actor and reason.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-discarded", 1)]
public sealed record MethodologyTemplateDraftDiscarded(Uuid TemplateId, Uuid DraftId, long Revision,
    Uuid DiscardedBy, string Reason) : DomainEvent;
