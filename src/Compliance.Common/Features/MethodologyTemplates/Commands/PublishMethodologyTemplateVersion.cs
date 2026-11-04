using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Answers the separate draft's exact committed terminal fact on its template line.</summary>
[Discriminator("bdgrz.methodology-templates.publish-methodology-template-version", 1)]
public sealed record PublishMethodologyTemplateVersion(Uuid TemplateId, Uuid DraftId, NativeFactReference Source,
    ulong SourcePosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
