using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Finalizes full practice commitments and source uses before reads, replay and append.</summary>
public sealed class PracticeCommitmentAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is PracticeIndependenceCommitment subject)
            _ = await PracticeCommitmentHistory.ReadAsync(events, subject, ct).ConfigureAwait(false);
        return owner;
    }
}
