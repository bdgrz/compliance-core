using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class RoleDirectoryProjector(IRoleDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "RoleDirectory"),
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleRenamed>,
      IProjectorHandler<RoleDeleted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleDefined> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleRenamed> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleDeleted> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
