using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Streams one exact resource without treating a quiet tail as current.</summary>
static class CriteriaPublicationSourceHead
{
    internal static async ValueTask<(ulong Next, bool Found)> ReadAsync(IDomainEventReader events, Aggregate owner,
        Uuid eventId, CancellationToken ct)
    {
        ulong next = 0;
        var found = false;
        await foreach (var item in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (item.Stream != owner.Stream || item.ResourceOffset != next || item.Event.Metadata.AggregateId != owner.Id ||
                item.Event.Metadata.EventId == Uuid.Empty || item.Event.Metadata.AggregateVersion == 0 || item.Event.Metadata.IsAudit)
                throw new CriteriaSourceUnavailableException("The exact publication participant source is incomplete.");
            next = checked(next + 1);
            if (item.Event.Metadata.EventId == eventId)
                found = true;
        }
        return (next, found);
    }
}
