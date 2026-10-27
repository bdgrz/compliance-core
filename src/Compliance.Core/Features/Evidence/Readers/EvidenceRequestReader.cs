using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Readers;

/// <summary>
///     Reads evidence requests from their own owner streams. Every request of a program is discovered from the opening
///     facts of the tenant and hydrated from its own stream, so nothing is read from a program-wide ledger and nothing is
///     retained as command state.
/// </summary>
public sealed class EvidenceRequestReader(IAggregateReader reader, IDomainEventReader events)
{
    /// <summary>The view of one request, or null when it was not opened.</summary>
    public async ValueTask<EvidenceRequestView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid requestId,
        CancellationToken ct = default) =>
        (await reader.HydrateAsync(new EvidenceRequest(tenantId, programId, requestId), ct).ConfigureAwait(false))
        .View();

    /// <summary>Every request of the program, in the order opened.</summary>
    public async ValueTask<EvidenceRequestSnapshot> ReadAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        var opened = new List<(DateTimeOffset At, Uuid Id)>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           EvidenceRequest.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is EvidenceRequestOpened ev &&
                ev.TenantId == tenantId && ev.ProgramId == programId)
                opened.Add((ev.OpenedAt, ev.EvidenceRequestId));

        var views = new List<EvidenceRequestView>();
        foreach (var (_, id) in opened.OrderBy(static item => item.At).ThenBy(static item => item.Id.ToString(),
                     StringComparer.Ordinal))
            if (await ReadViewAsync(tenantId, programId, id, ct).ConfigureAwait(false) is { } view)
                views.Add(view);
        return new EvidenceRequestSnapshot(views);
    }
}
