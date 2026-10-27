using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.acceptance-history.get", 1)]
public sealed record GetServiceEngagementAcceptanceHistory(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<ServiceEngagementAcceptanceView>>, IIndependenceAdministrationRequest, ICallable;
