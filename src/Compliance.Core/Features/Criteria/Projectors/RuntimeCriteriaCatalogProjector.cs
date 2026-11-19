using Bdgrz.Compliance.Features.Criteria.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projectors;

/// <summary>Indexes the independently owned criteria facts in source order.</summary>
sealed class RuntimeCriteriaCatalogProjector(IRuntimeCriteriaCatalogProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz"), WorkloadName)
{
    internal const string WorkloadName = "RuntimeCriteriaCatalogV1";

    protected override async ValueTask<Result> ProjectEventAsync(DomainEventRecord record,
        IProjectorContext context, CancellationToken ct)
    {
        await projection.ApplyAsync(record, context, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
