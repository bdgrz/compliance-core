using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Commits the first attempt but reports it as unknown, as a timeout after append would.</summary>
sealed class CommitThenFailOnceExecutor(IAggregateExecutor inner) : IAggregateExecutor
{
    int _calls;

    public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
        where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

    public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
        where TAggregate : Aggregate
    {
        var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
        if (Interlocked.Increment(ref _calls) == 1)
            throw new EventStreamConcurrencyException("Append outcome unknown.", new TimeoutException());
        return result;
    }
}
