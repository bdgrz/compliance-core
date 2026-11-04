using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Creates the idle line only from its exact admitted name claim and original authoring intent.</summary>
[Discriminator("bdgrz.methodology-templates.create-methodology-template-line", 1)]
public sealed record CreateMethodologyTemplateLine(Uuid TemplateId, NativeFactReference Claim,
    ulong ClaimPosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
