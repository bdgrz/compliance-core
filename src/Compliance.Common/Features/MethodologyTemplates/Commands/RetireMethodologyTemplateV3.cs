using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Retires an idle methodology line with the personal operator's reason.</summary>
[Discriminator("bdgrz.methodology-templates.retire-methodology-template", 3)]
public sealed record RetireMethodologyTemplateV3(Uuid CommandId, Uuid TemplateId, string Reason)
    : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    public const string Permission = "methodology.approve";
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
