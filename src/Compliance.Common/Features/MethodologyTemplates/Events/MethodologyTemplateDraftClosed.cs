using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The line closed its open draft from that draft's committed discard and reason.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-closed", 1)]
public sealed record MethodologyTemplateDraftClosed(Uuid TemplateId, Uuid DraftId, Uuid DiscardEventId, string Reason) : DomainEvent;
