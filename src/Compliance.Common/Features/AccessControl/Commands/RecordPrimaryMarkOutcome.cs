using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.record-primary-mark-outcome", 1)]
public sealed record RecordPrimaryMarkOutcome(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId) : IRequest, IRbacManagementRequest;
