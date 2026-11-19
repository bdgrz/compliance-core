using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Events;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Readers;

/// <summary>Discovers records from source and hydrates each authoritative lifecycle, including voids.</summary>
sealed class NonattestServiceRecordReader(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<IReadOnlyList<NonattestServiceRecordView>> ReadAllAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty)
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        var ids = new HashSet<Uuid>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), NonattestServiceRecord.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Stream.Realm != tenantId.ToString() || record.Stream.Area != NonattestServiceRecord.Area ||
                !Uuid.TryParse(record.Stream.Resource, CultureInfo.InvariantCulture, out var id) || id == Uuid.Empty ||
                record.Event is not (NonattestServiceRecorded or NonattestServiceRevised or NonattestServiceVoided))
                throw new InvalidOperationException("The nonattest source inventory contains another scope or unknown fact.");
            ids.Add(id);
        }
        var result = new List<NonattestServiceRecordView>(ids.Count);
        foreach (var id in ids.OrderBy(static value => value.ToString(), StringComparer.Ordinal))
        {
            var owner = await reader.HydrateAsync(new NonattestServiceRecord(tenantId, id), ct).ConfigureAwait(false);
            if (owner.View is not { } view)
                throw new InvalidOperationException("A discovered service record has no authoritative state.");
            result.Add(view);
        }
        return result.AsReadOnly();
    }
}
