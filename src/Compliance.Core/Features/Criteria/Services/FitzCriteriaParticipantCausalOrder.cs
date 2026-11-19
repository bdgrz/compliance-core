using Bdgrz.Compliance.Features.Criteria.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Orders two exact committed participant facts using Fitz realm positions.</summary>
/// <remarks>Relies on Fitz's committed monotonic realm order in the configured broker.</remarks>
public sealed class FitzCriteriaParticipantCausalOrder(ICriteriaExactSourceReader source) : ICriteriaParticipantCausalOrder
{
    /// <inheritdoc />
    public async ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Stream.Realm != second.Stream.Realm || first.RealmOffset is not { } before ||
            second.RealmOffset is not { } after || before >= after || first.Event.Metadata.EventId == Uuid.Empty ||
            second.Event.Metadata.EventId == Uuid.Empty || first.Event.Metadata.AggregateId == Uuid.Empty ||
            second.Event.Metadata.AggregateId == Uuid.Empty)
            return false;
        var actualFirst = await source.FindAsync(first.Stream, first.Event.Metadata.AggregateId,
            first.Event.Metadata.EventId, ct).ConfigureAwait(false);
        var actualSecond = await source.FindAsync(second.Stream, second.Event.Metadata.AggregateId,
            second.Event.Metadata.EventId, ct).ConfigureAwait(false);
        return actualFirst == first && actualSecond == second;
    }
}
