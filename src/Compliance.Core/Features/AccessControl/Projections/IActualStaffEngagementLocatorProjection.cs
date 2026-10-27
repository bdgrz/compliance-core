using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projections;

interface IActualStaffEngagementLocatorProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEventRecord source, IProjectorContext context, CancellationToken ct);
}
