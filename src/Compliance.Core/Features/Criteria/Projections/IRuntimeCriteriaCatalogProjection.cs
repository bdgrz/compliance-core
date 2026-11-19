using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projections;

interface IRuntimeCriteriaCatalogProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEventRecord source, IProjectorContext context, CancellationToken ct);
}
