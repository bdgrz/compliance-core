using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.access-grant.list", 1)]
public sealed record ListAccessGrants(Uuid TenantId)
    : IRequest<AccessGrantSetView>, IRbacManagementRequest, ICallable;
