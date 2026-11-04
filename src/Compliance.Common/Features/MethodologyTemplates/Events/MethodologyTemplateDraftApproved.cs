using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>A third person approved the immutable independently accepted revision.</summary>
[Discriminator("bdgrz.methodology-templates.methodology-template-draft-approved", 1)]
public sealed record MethodologyTemplateDraftApproved(Uuid TemplateId, Uuid DraftId, long Revision,
    ImmutableContentRef EntriesContent, Uuid ApprovedBy) : DomainEvent;
