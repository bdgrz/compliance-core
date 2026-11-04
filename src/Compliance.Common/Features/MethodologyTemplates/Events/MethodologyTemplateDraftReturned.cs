using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>An independent reviewer returned the current revision with comments.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-returned", 1)]
public sealed record MethodologyTemplateDraftReturned(Uuid TemplateId, Uuid DraftId, long Revision,
    Uuid ReviewedBy, string Comments) : DomainEvent;
