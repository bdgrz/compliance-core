using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Verifies the complete native Access owner prefix before an installed read or append.</summary>
public sealed class AccessReviewAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is AccessExpectation expectation)
            _ = await AccessExpectationHistory.ReadAsync(events, expectation, ct).ConfigureAwait(false);
        else if (owner is AccessReviewCampaign campaign)
            _ = await AccessReviewCampaignHistory.ReadAsync(events, campaign, ct).ConfigureAwait(false);
        else if (owner is AccessReviewItem item)
            _ = await AccessReviewItemHistory.ReadAsync(events, item, ct).ConfigureAwait(false);
        return owner;
    }
}
