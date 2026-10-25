using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Finalizes the exact native client service prefix for explicit reads, accepted retries and executor hydration.</summary>
public sealed class ProductClientServiceAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        TAggregate owner;
        try
        {
            owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
            if (owner is ClientService service)
                _ = await ProductClientServiceHistory.ReadAsync(events, service, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return owner;
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
        catch (InvalidOperationException error) when (aggregate is ClientService)
        {
            ct.ThrowIfCancellationRequested();
            throw new ImmutableContentIntegrityException("The client service cannot hydrate its native transition: " + error.Message);
        }
    }
}
