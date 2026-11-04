using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>An independent reviewer accepted the exact current revision.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-accepted", 1)]
public sealed record MethodologyTemplateDraftAccepted(Uuid TemplateId, Uuid DraftId, long Revision,
    Uuid ReviewedBy, string Comments) : DomainEvent;
