using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Finalizes calendar raw transition validation for direct reads and executor-internal hydration alike.</summary>
public sealed class TypeIICalendarAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    /// <inheritdoc />
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is TypeIIObservationCalendar calendar)
            _ = await TypeIICalendarNativeHistory.ReadAsync(calendar, events, ct).ConfigureAwait(false);
        return hydrated;
    }
}
