using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Finalizes the same complete native authority for public reads and executor-internal rehydration.</summary>
public sealed class AdvisorAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is AdvisorHandoff or AdvisorFeedbackThread)
            _ = await AdvisorNativeHistory.ReadAsync(hydrated, events, ct).ConfigureAwait(false);
        return hydrated;
    }
}
