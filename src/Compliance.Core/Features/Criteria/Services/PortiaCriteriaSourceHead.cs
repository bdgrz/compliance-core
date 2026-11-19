using Bdgrz.Compliance.Features.Criteria.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Compares an exact resource against Portia's committed contiguous source.</summary>
/// <remarks>
/// This is a point-in-time observation, not a lock. The decision must still append at
/// the observed expected position, and an unknown append must be reconciled or fenced.
/// It relies on the configured Portia/Fitz committed-read and expected-position contracts.
/// </remarks>
public sealed class PortiaCriteriaSourceHead(IDomainEventReader events) : ICriteriaFreshEntryHead,
    ICriteriaFreshEditionHead, ICriteriaFreshLabelClaimHead
{
    /// <inheritdoc />
    public async ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (aggregateId == Uuid.Empty)
            return false;
        ulong next = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.ResourceOffset != next || record.Event.Metadata.AggregateId != aggregateId ||
                record.Event.Metadata.EventId == Uuid.Empty || record.Event.Metadata.IsAudit ||
                record.Event.Metadata.AggregateVersion != checked(next + 1))
                return false;
            next = checked(next + 1);
            if (next > nextResourceOffset)
                return false;
        }
        return next == nextResourceOffset;
    }
}
