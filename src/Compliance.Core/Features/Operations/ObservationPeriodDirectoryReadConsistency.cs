using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Uses the normal projection checkpoint fence before and after composing a bounded package page.</summary>
public sealed class ObservationPeriodDirectoryReadConsistency(IObservationPeriodDirectoryReader directory, IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return await HasPendingAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(Behind()) : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId, ProjectionCheckpoint fence,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return checkpoint != fence || await HasPendingAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result.Failure(Behind()) : Result.Success;
    }

    async ValueTask<bool> HasPendingAsync(Uuid tenantId, ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            TypeIIPeriod.Area), checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError Behind() => new(RequestErrorKind.Conflict,
        "The observation-period directory changed or has not reached its source. Retry the query.", isTransient: true);
}
