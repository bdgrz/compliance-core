using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Prepares a receiver after its actual native receipt decision, recovering equal winners on expected failure.</summary>
public static class NativeReceiverPreparation
{
    /// <summary>
    /// Resolves native receipt knowledge before preparation and rechecks it after a failed result or expected immutable
    /// failure. The caller owns receipt semantics and classification. Requested cancellation and programmer faults pass through.
    /// </summary>
    public static async ValueTask<Result<Prepared<TPrepared>>> PrepareAsync<TOwner, TPrepared>(IAggregateReader reader,
        Func<TOwner> open, Func<TOwner, Result<ReceiverOutcome>?> decide,
        Func<TOwner, CancellationToken, ValueTask<Result<TPrepared>>> prepare, CancellationToken ct)
        where TOwner : Aggregate where TPrepared : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(decide);
        ArgumentNullException.ThrowIfNull(prepare);
        ct.ThrowIfCancellationRequested();
        var owner = await reader.HydrateAsync(open(), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var known = decide(owner);
        ct.ThrowIfCancellationRequested();
        if (known is not null)
            return Result<Prepared<TPrepared>>.Success(new Prepared<TPrepared>(null, known));
        Result<TPrepared> candidate;
        try
        {
            candidate = await prepare(owner, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            var winner = await RecheckAsync(reader, open, decide, ct).ConfigureAwait(false);
            if (winner is not null)
                return Result<Prepared<TPrepared>>.Success(new Prepared<TPrepared>(null, winner));
            throw;
        }
        ct.ThrowIfCancellationRequested();
        if (!candidate.IsSuccess)
        {
            var winner = await RecheckAsync(reader, open, decide, ct).ConfigureAwait(false);
            return winner is not null
                ? Result<Prepared<TPrepared>>.Success(new Prepared<TPrepared>(null, winner))
                : Result<Prepared<TPrepared>>.Failure(candidate.Error!);
        }
        return Result<Prepared<TPrepared>>.Success(new Prepared<TPrepared>(candidate.Value ??
            throw new InvalidOperationException("Successful receiver preparation must provide its candidate."), null));
    }

    static async ValueTask<Result<ReceiverOutcome>?> RecheckAsync<TOwner>(IAggregateReader reader, Func<TOwner> open,
        Func<TOwner, Result<ReceiverOutcome>?> decide, CancellationToken ct) where TOwner : Aggregate
    {
        var owner = await reader.HydrateAsync(open(), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var outcome = decide(owner);
        ct.ThrowIfCancellationRequested();
        return outcome;
    }

    /// <summary>Exactly one prepared candidate or already decided native receipt outcome.</summary>
    public sealed record Prepared<TPrepared>(TPrepared? Value, Result<ReceiverOutcome>? Outcome) where TPrepared : class;
}
