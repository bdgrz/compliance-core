using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records an exact committed identity conflict and releases only its losing name generation.</summary>
[Discriminator("bdgrz.methodology-templates.refuse-methodology-template-creation", 1)]
public sealed record RefuseMethodologyTemplateCreation(Uuid TemplateId, NativeFactReference Claim, ulong ClaimPosition,
    NativeFactReference ExistingCreation, ulong ExistingPosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
