using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Pins one destination's approved base before an idempotent proposal dispatch.</summary>
[Discriminator("bdgrz.methodology-templates.template-entry-proposal-prepared", 1)]
public sealed record TemplateEntryProposalPrepared(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationEventId, TemplateEntryProposalBase ProposalBase) : DomainEvent;
