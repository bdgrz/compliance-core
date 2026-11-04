using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The line start opened one separate draft with its exact immutable initial entries.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-opened", 1)]
public sealed record MethodologyTemplateDraftOpened(Uuid TemplateId, Uuid DraftId, Uuid StartEventId, long BasedOnVersion,
    ImmutableContentRef EntriesContent, IReadOnlyList<string> EntryKeys) : DomainEvent;
