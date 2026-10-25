using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Verifies expectation-line native prefixes before an installed read or append.</summary>
public sealed class AccessReviewAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is AccessExpectation expectation)
            _ = await AccessExpectationHistory.ReadAsync(events, expectation, ct).ConfigureAwait(false);
        return owner;
    }
}
