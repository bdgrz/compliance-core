using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.get", 1)]
public sealed record GetServiceEngagement(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;
