using Cntryl.Portia;
using Bdgrz.Compliance.Features.SourceFacts;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Finalizes full actual source prefix before any technical or operator mutation can append.</summary>
public sealed class WorkDigestAggregateReader(IAggregateReader inner, IDomainEventReader events, ISourceFactResolver sources) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is WorkDigestDispatch dispatch)
            _ = await WorkDigestDispatchHistory.ReadAsync(events, dispatch, ct).ConfigureAwait(false);
        else if (hydrated is WeeklyWorkDigest weekly)
            _ = await WeeklyWorkDigestHistory.ReadAsync(events, sources, weekly, ct).ConfigureAwait(false);
        return hydrated;
    }
}
