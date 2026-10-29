using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Processes;

/// <summary>Prepares a receiver after its actual native receipt decision, recovering equal winners on expected failure.</summary>
public static class NativeReceiverPreparation
{
    /// <summary>
    /// Resolves native receipt knowledge before preparation and rechecks it after a failed result or expected immutable
    /// failure. The caller owns receipt semantics and classification. Requested cancellation and programmer faults pass through.
    /// </summary>
    public static ValueTask<Result<Prepared<TPrepared>>> PrepareAsync<TOwner, TPrepared>(IAggregateReader reader,
        Func<TOwner> open, Func<TOwner, Result<ReceiverOutcome>?> decide,
        Func<TOwner, CancellationToken, ValueTask<Result<TPrepared>>> prepare, CancellationToken ct)
        where TOwner : Aggregate where TPrepared : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(decide);
        ArgumentNullException.ThrowIfNull(prepare);
        return PrepareAsync(reader, open, (owner, _) => ValueTask.FromResult(decide(owner)), prepare, ct);
    }

    /// <summary>The same recovery boundary when receipt terms require a verified asynchronous selected-body bind.</summary>
    public static async ValueTask<Result<Prepared<TPrepared>>> PrepareAsync<TOwner, TPrepared>(IAggregateReader reader,
        Func<TOwner> open, Func<TOwner, CancellationToken, ValueTask<Result<ReceiverOutcome>?>> decide,
        Func<TOwner, CancellationToken, ValueTask<Result<TPrepared>>> prepare, CancellationToken ct)
        where TOwner : Aggregate where TPrepared : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(decide);
        ArgumentNullException.ThrowIfNull(prepare);
        ct.ThrowIfCancellationRequested();
        var owner = await ReadAsync(reader, open, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var known = await DecideAsync(owner, decide, ct).ConfigureAwait(false);
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
        Func<TOwner, CancellationToken, ValueTask<Result<ReceiverOutcome>?>> decide, CancellationToken ct) where TOwner : Aggregate
    {
        var owner = await ReadAsync(reader, open, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var outcome = await DecideAsync(owner, decide, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return outcome;
    }

    static async ValueTask<TOwner> ReadAsync<TOwner>(IAggregateReader reader, Func<TOwner> open, CancellationToken ct)
        where TOwner : Aggregate
    {
        try
        {
            return await reader.HydrateAsync(open(), ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
    }

    static async ValueTask<Result<ReceiverOutcome>?> DecideAsync<TOwner>(TOwner owner,
        Func<TOwner, CancellationToken, ValueTask<Result<ReceiverOutcome>?>> decide, CancellationToken ct)
    {
        try
        {
            return await decide(owner, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
    }

    /// <summary>Exactly one prepared candidate or already decided native receipt outcome.</summary>
    public sealed record Prepared<TPrepared>(TPrepared? Value, Result<ReceiverOutcome>? Outcome) where TPrepared : class;
}
