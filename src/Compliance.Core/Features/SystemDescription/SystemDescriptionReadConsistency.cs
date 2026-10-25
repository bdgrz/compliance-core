using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Authoritative scoped native/physical source fence; no missing projection is mistaken for absence.</summary>
public sealed class SystemDescriptionReadConsistency(IAggregateReader reader)
{
    public async ValueTask<Result> ConfirmAsync(SystemDescriptionReadSnapshot snapshot, CancellationToken ct)
    {
        var current = await reader.HydrateAsync(new SystemDescription(snapshot.TenantId, snapshot.ProgramId, snapshot.Id), ct).ConfigureAwait(false);
        return current.NativeSequence == snapshot.Owner.NativeSequence && current.CommittedStreamPosition == snapshot.Owner.CommittedStreamPosition
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The description source changed while its response was composed. Retry the query.", true));
    }
}
