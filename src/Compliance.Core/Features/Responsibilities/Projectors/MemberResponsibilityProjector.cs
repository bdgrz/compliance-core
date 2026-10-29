using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Services;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Responsibilities.Projectors;

public sealed partial class MemberResponsibilityProjector(FitzMemberResponsibilityIndex projection)
    : Projector(projection, EventStreamPattern.ForTenant(), FitzMemberResponsibilityIndex.ProjectorName),
      IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public ValueTask HandleAsync(ResponsibilityAssigned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
