using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Commits through the inner executor and then loses the response once, so the caller cannot tell whether the
///     append happened: the unknown append outcome a retry must resolve without writing twice.
/// </summary>
sealed class LostResponseExecutor(IAggregateExecutor inner) : IAggregateExecutor
{
    bool _lost;

    public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
        return Lose(result);
    }

    public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
        Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
        return Lose(result);
    }

    TResult Lose<TResult>(TResult result)
    {
        if (_lost)
            return result;
        _lost = true;
        throw new TimeoutException("The append response was lost.");
    }
}
