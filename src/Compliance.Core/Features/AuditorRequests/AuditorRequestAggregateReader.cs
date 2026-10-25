using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Validates the entire exact native prefix on every installed hydration, before accepted retry or fresh work.</summary>
public sealed class AuditorRequestAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is AuditorRequest request)
            _ = await AuditorRequestHistory.ReadAsync(events, request, ct).ConfigureAwait(false);
        return owner;
    }
}
