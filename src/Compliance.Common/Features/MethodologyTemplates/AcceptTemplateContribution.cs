using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Accepts a reviewed contribution into platform methodology.</summary>
[Discriminator("bdgrz.methodology-templates.accept-template-contribution", 1)]
public sealed record AcceptTemplateContribution(Uuid TenantId, Uuid ContributionId,
    string AcceptanceRationale) : IRequest, IPlatformOperatorRequest, ICallable;
