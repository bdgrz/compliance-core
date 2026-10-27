using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class AccessGrantProjector(IAccessGrantProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "AccessGrantsV1"),
      IProjectorHandler<AccessGrantIssued>, IProjectorHandler<AccessGrantRevoked>
{
    public ValueTask HandleAsync(AccessGrantIssued ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessGrantRevoked ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
