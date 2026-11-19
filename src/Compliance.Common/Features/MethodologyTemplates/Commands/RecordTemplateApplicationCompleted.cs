using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Closes the in-flight ordering decision using its exact committed version-completion fact.</summary>
[Discriminator("bdgrz.methodology-templates.record-template-application-completed", 1)]
public sealed record RecordTemplateApplicationCompleted(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, NativeFactReference Source, ulong SourcePosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
