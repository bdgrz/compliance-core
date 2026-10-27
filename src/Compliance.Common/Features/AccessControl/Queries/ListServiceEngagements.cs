using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.list", 1)]
public sealed record ListServiceEngagements(Uuid TenantId)
    : IRequest<IReadOnlyList<ServiceEngagementView>>, IIndependenceAdministrationRequest, ICallable;
