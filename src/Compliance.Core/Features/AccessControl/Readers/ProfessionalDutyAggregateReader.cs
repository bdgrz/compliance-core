using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Finalizes full designation receipts before current reads, native retries or appends.</summary>
public sealed class ProfessionalDutyAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is FirmProfessionalDutyDesignation designation)
            _ = await ProfessionalDutyHistory.ReadAsync(events, designation, ct).ConfigureAwait(false);
        return owner;
    }
}
