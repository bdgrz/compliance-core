using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class TeamRoleDirectoryProjector(ITeamRoleDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "TeamRoleDirectory"),
      IProjectorHandler<TeamRoleAssigned>,
      IProjectorHandler<TeamRoleRemoved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamRoleAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamRoleRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
