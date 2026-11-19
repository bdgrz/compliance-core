using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.mark-member-primary", 1)]
public sealed record MarkMemberPrimary(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId) : IRequest, IRbacManagementRequest;
