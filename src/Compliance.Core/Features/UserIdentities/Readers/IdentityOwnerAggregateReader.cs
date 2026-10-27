using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

/// <summary>Finalizes complete identity and email prefixes for explicit reads and executor-internal hydration.</summary>
public sealed class IdentityOwnerAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is UserIdentity identity)
            _ = await UserIdentityHistory.ReadAsync(events, identity, ct).ConfigureAwait(false);
        else if (owner is EmailAddress address)
            _ = await EmailAddressHistory.ReadAsync(events, address, ct).ConfigureAwait(false);
        return owner;
    }
}
