using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class RolePermissionDirectoryProjector(IRolePermissionDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "RolePermissionDirectory"),
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<RolePermissionRemoved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<RolePermissionAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RolePermissionRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
