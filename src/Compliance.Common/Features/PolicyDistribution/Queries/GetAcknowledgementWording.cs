using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Queries;

[Discriminator("bdgrz.policy.acknowledgement-wording.get", 1)]
public sealed record GetAcknowledgementWording(Uuid TenantId)
    : IRequest<AcknowledgementWordingView>, ITenantAccessRequest, ICallable;
