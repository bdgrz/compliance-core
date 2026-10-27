using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Fences tenant program enumeration against program creation and revision.</summary>
public sealed class ProgramDirectoryReadConsistency(IProgramDirectoryReader directory,
    IDomainEventReader events)
{
    const string ProgramArea = "programs";

    public async ValueTask<Result<ProjectionCheckpoint>> CaptureListFenceAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return await HasPendingProgramSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> EnsureListFenceHeldAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return checkpoint != fence ||
               await HasPendingProgramSourceAsync(tenantId, checkpoint, ct)
                   .ConfigureAwait(false)
            ? Result.Failure(BehindSourceError())
            : Result.Success;
    }

    async ValueTask<bool> HasPendingProgramSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                tenantId.ToString(), ProgramArea), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        while (await pending.MoveNextAsync().ConfigureAwait(false))
        {
            if (pending.Current.Event is ProgramCreated or ProgramRevised or
                ProgramCriteriaEditionSelected)
                return true;
        }
        return false;
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The program directory changed or has not reached the source. Retry the query.",
        isTransient: true);
}
