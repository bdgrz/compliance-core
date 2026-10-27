using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Readers;

/// <summary>Finalizes the same complete native authority for explicit reads and executor-internal hydration.</summary>
public sealed class PolicyCampaignAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is PolicyDistributionCampaign or PolicyCampaignParticipant)
            _ = await PolicyCampaignHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        return owner;
    }
}
