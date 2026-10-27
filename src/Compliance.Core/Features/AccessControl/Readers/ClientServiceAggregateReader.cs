using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Verifies full client service/use history before current reads, accepted retries and appends.</summary>
public sealed class ClientServiceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ClientServiceIndependence client)
            _ = await ClientServiceNativeHistory.ReadAsync(events, client, ct).ConfigureAwait(false);
        else if (owner is ClientIndependenceDecision decision)
            await ClientServiceNativeHistory.VerifyDecisionAsync(events, decision, ct).ConfigureAwait(false);
        return owner;
    }
}
