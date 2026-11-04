using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class TeamMemberDirectoryProjector(ITeamMemberDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "TeamMemberDirectory"),
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<TeamMemberRemoved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamMemberAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamMemberRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
