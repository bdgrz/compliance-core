using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Finalizes staff native and use history before current reads, retries and appends.</summary>
public sealed class FirmStaffAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is FirmStaffRecord staff)
            _ = await FirmStaffHistory.ReadAsync(events, staff, ct).ConfigureAwait(false);
        else if (owner is FirmStaffIdentityClaim claim)
            await FirmStaffHistory.VerifyClaimAsync(events, claim, ct).ConfigureAwait(false);
        return owner;
    }
}
