using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.member.access.get", 1)]
public sealed record GetMemberAccess(Uuid TenantId, Uuid UserId,
    string? ExpectedBuiltInRole = null)
    : IRequest<MemberAccessView>, IRbacManagementRequest, ICallable;
