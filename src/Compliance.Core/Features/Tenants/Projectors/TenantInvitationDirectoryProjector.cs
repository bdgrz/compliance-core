using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class TenantInvitationDirectoryProjector(
    ITenantInvitationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("tenant-invitations"),
      "TenantInvitationDirectory"),
      IProjectorHandler<TenantMemberInvited>, IProjectorHandler<TenantInvitationAccepted>
{
    public ValueTask HandleAsync(TenantMemberInvited ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantInvitationAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
