using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Fences discovery against the exact relevant tenant request stream pattern before and after native reads.</summary>
public sealed class AuditorRequestDirectoryReadConsistency(IAuditorRequestDirectoryReader directory, IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return await HasPendingAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(Behind()) : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmAsync(Uuid tenantId, ProjectionCheckpoint before, CancellationToken ct)
    {
        var after = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return after != before || await HasPendingAsync(tenantId, after, ct).ConfigureAwait(false)
            ? Result.Failure(Behind()) : Result.Success;
    }

    async ValueTask<bool> HasPendingAsync(Uuid tenantId, ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(), AuditorRequest.Area),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError Behind() => new(RequestErrorKind.Conflict,
        "The auditor-request directory changed or has not reached its exact source. Retry the read.", true);
}
