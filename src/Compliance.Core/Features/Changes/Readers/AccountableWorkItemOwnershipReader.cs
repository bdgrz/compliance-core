using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Readers;

sealed class AccountableWorkItemOwnershipReader(
    IEnumerable<IAccountableWorkItemDirectoryReader> directories,
    WorkQueueReadConsistency consistency) : IChangeImpactWorkOwnershipReader
{
    readonly IAccountableWorkItemDirectoryReader[] _directories = directories
        .OrderBy(static directory => directory.ProjectorName, StringComparer.Ordinal).ToArray();

    public async ValueTask<Result> ConfirmOwnedByProgramAsync(Uuid tenantId, Uuid programId,
        IReadOnlyCollection<Uuid> workItemIds, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || workItemIds is null ||
            workItemIds.Any(static id => id == Uuid.Empty) ||
            workItemIds.Distinct().Count() != workItemIds.Count)
            return InvalidWorkScope();
        if (workItemIds.Count == 0)
            return Result.Success;

        var captured = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result.Failure(captured.Error);

        var expected = workItemIds.ToHashSet();
        var owners = new Dictionary<Uuid, WorkCandidate>();
        foreach (var directory in _directories)
        {
            var projected = await directory.LoadProgramAsync(tenantId, programId, ct)
                .ConfigureAwait(false);
            if (!projected.IsSuccess)
                return Result.Failure(projected.Error);

            foreach (var candidate in projected.Value.Where(candidate =>
                         expected.Contains(candidate.WorkItemId)))
            {
                if (!owners.TryAdd(candidate.WorkItemId, candidate))
                    return InvalidWorkScope();
            }
        }

        var caughtUp = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            captured.Value, ct).ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return caughtUp;
        return owners.Count == expected.Count
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "One or more impact-review work items are not present in the current tenant and program work sources.",
                isTransient: true));
    }

    static Result InvalidWorkScope() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Impact-review work ownership is ambiguous or outside the requested scope."));
}
