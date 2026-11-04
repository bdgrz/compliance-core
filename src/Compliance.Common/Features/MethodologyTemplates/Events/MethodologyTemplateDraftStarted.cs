using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The template line selected its one open draft and the immutable version entries it starts from.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-started", 1)]
public sealed record MethodologyTemplateDraftStarted(Uuid TemplateId, Uuid DraftId, long BasedOnVersion,
    ImmutableContentRef EntriesContent, Uuid StartedBy) : DomainEvent;
