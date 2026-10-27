using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.list", 1)]
public sealed record ListServiceEngagements(Uuid TenantId)
    : IRequest<IReadOnlyList<ServiceEngagementView>>, IIndependenceAdministrationRequest, ICallable;
