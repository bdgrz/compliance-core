using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.acceptance.get", 1)]
public sealed record GetServiceEngagementAcceptance(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementAcceptanceView>, IIndependenceAdministrationRequest, ICallable;
