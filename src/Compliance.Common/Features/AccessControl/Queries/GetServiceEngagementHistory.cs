using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.history.get", 1)]
public sealed record GetServiceEngagementHistory(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<ServiceEngagementView>>, IIndependenceAdministrationRequest, ICallable;
