using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Accepts a reviewed contribution into platform methodology.</summary>
[Discriminator("bdgrz.methodology-templates.accept-template-contribution", 2)]
public sealed record AcceptTemplateContribution(Uuid CommandId, Uuid TenantId, Uuid ContributionId,
    string AcceptanceRationale) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
