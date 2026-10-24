using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Holds hydrations of one aggregate type until every racer has loaded the stream.</summary>
sealed class HydrationGate<TOwner>(IAggregateReader inner, int participants) : IAggregateReader where TOwner : Aggregate
{
    readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int _arrived;

    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
        CancellationToken ct = default) where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct);
        if (hydrated is not TOwner)
            return hydrated;
        if (Interlocked.Increment(ref _arrived) >= participants)
            _released.TrySetResult();
        await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        return hydrated;
    }
}
