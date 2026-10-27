using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.tenant.member.get", 1)]
public sealed record GetTenantMember(Uuid TenantId, Uuid UserId)
    : IRequest<TenantMembershipView>, IRbacManagementRequest, ICallable;
