using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class RolePermissionDirectoryProjector(IRolePermissionDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "RolePermissionDirectory"),
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<RolePermissionRemoved>
{
    public ValueTask HandleAsync(RolePermissionAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RolePermissionRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
