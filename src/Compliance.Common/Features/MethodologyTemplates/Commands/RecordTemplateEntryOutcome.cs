using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records one owning-context outcome for an entry of the exact opened application.</summary>
[Discriminator("bdgrz.methodology-templates.record-template-entry-outcome", 1)]
public sealed record RecordTemplateEntryOutcome(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, Uuid ApplicationEventId, TemplateEntryOutcome Outcome, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
