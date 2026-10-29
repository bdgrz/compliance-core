using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Queries;

[Discriminator("bdgrz.member.responsibilities.list", 1)]
public sealed record ListMemberResponsibilities(Uuid TenantId, Uuid UserId)
    : IRequest<IReadOnlyList<ResponsibilityAssignmentView>>, IRbacManagementRequest, ICallable;
