using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.member.access.get", 1)]
public sealed record GetMemberAccess(Uuid TenantId, Uuid UserId,
    string? ExpectedBuiltInRole = null)
    : IRequest<MemberAccessView>, IRbacManagementRequest, ICallable;
