using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>Checks the full native prefix before reads, original receipt resolution and writes.</summary>
public sealed class WorkAssignmentAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is WorkItemAssignment assignment)
            _ = await WorkAssignmentNativeHistory.ReadAsync(events, assignment, ct).ConfigureAwait(false);
        return owner;
    }
}
