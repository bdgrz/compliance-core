using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class TeamDirectoryProjector(ITeamDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "TeamDirectory"),
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>
{
    public ValueTask HandleAsync(TeamDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
