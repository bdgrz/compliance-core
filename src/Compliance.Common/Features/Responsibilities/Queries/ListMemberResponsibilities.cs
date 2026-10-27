using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities.Queries;

[Discriminator("bdgrz.member.responsibilities.list", 1)]
public sealed record ListMemberResponsibilities(Uuid TenantId, Uuid UserId)
    : IRequest<IReadOnlyList<ResponsibilityAssignmentView>>, IRbacManagementRequest, ICallable;
