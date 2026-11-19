using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Reads committed records from one exact source with their broker area positions.</summary>
public interface ICriteriaExactSourceReader
{
    /// <summary>Finds one event; null means only that it was not observed.</summary>
    ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
        Uuid eventId, CancellationToken ct = default);

    /// <summary>Reads one exact resource without asserting fresh-to-head completeness.</summary>
    IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
        CancellationToken ct = default);
}
