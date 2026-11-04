using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Opens the entry outcome record from an exact committed template application.</summary>
[Discriminator("bdgrz.methodology-templates.open-template-version-application", 1)]
public sealed record OpenTemplateVersionApplication(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, NativeFactReference Source, ulong SourcePosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
