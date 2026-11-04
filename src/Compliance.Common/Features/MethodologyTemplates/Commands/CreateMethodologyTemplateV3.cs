using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Admits an original platform methodology line under an unused normalized name.</summary>
[Discriminator("bdgrz.methodology-templates.create-methodology-template", 3)]
public sealed record CreateMethodologyTemplateV3(Uuid CommandId, Uuid TemplateId, string Name, string Purpose)
    : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
