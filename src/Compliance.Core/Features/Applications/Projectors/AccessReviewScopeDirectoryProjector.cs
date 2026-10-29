using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Projections;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class AccessReviewScopeDirectoryProjector(
    IAccessReviewScopeDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewScopeStreams.Area),
            AccessReviewScopeStreams.ProjectorName),
        IProjectorHandler<AccessReviewScopeDecided>
{
    public ValueTask HandleAsync(AccessReviewScopeDecided ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
