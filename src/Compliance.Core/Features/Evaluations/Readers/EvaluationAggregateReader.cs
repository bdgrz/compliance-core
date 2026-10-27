using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Readers;

/// <summary>Finalizes the full native evaluation prefix before explicit or executor-internal hydration returns.</summary>
public sealed class EvaluationAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is ControlEvaluation evaluation)
            _ = await ControlEvaluationHistory.ReadAsync(events, evaluation, ct).ConfigureAwait(false);
        return owner;
    }
}
