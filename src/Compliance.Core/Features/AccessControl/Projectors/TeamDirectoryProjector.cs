using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class TeamDirectoryProjector(ITeamDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "TeamDirectory"),
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamDefined> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamDeleted> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
