using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Opens a version's exact planned outcomes from a committed canonical ordering decision.</summary>
[Discriminator("bdgrz.methodology-templates.open-template-version-application", 4)]
public sealed record OpenTemplateVersionApplicationV4(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, NativeFactReference Source, ulong SourcePosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
