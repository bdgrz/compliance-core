using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.access-grant.list", 1)]
public sealed record ListAccessGrants(Uuid TenantId)
    : IRequest<AccessGrantSetView>, IRbacManagementRequest, ICallable;
