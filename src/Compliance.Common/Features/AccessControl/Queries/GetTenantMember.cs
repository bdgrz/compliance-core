using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.tenant.member.get", 1)]
public sealed record GetTenantMember(Uuid TenantId, Uuid UserId)
    : IRequest<TenantMembershipView>, IRbacManagementRequest, ICallable;
