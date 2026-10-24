using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
/// Freezes the persisted, approved close reconciliation. The frozen fact names the exact live calendar reservation
/// of the period; <see cref="TypeIIRangeReleaseReactor" /> releases that reservation from it afterwards.
/// </summary>
public sealed class FreezeTypeIIPeriodCloseHandler(IAggregateExecutor executor,
    IAggregateReader reader, ManagementExecutiveAuthority executiveAuthority, TimeProvider clock)
    : IRequestHandler<FreezeTypeIIPeriodClose>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<FreezeTypeIIPeriodClose> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out _) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Freezing a Type II close requires a personal HTTP actor."));
        if (request.ExpectedRevision <= 0 || request.CloseSnapshotId == Uuid.Empty)
            return Invalid("A Type II close freeze requires a positive revision and stable snapshot identity.");

        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, ct).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Freezing a Type II close requires one current program-scoped CEO or CISO permission."));

        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.PeriodId), ct).ConfigureAwait(false);
        if (period.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type II observation period was not found."));
        var approval = current.CloseApprovals.Count == 0 ? null : current.CloseApprovals[^1];
        if (current.CloseValidation is not { Status: "ready" } validation || approval is null)
            return Conflict("Freezing the Type II close requires its current reconciliation and executive approval.");
        if (approval.ApprovedByMemberId != memberId ||
            !StringComparer.OrdinalIgnoreCase.Equals(approval.ApproverRole, role))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the same currently authorized executive who approved the close can freeze it."));

        var content = new TypeIIPeriodCloseSnapshotContent(request.CloseSnapshotId,
            validation.SourceReferences, validation.ExpectedOccurrenceIds,
            approval.AcknowledgedMaterialItemIds);
        var frozenAt = clock.GetUtcNow();
        var existing = current.CloseSnapshots.FirstOrDefault(item =>
            item.CloseSnapshotId == request.CloseSnapshotId);
        if (existing is not null && !MatchesFrozenSnapshot(existing, content, memberId, role))
            return Conflict("The close snapshot identity already contains different frozen facts.");

        if (existing is null)
        {
            if (period.FreezeClose(request.ExpectedRevision, content, memberId, role,
                    frozenAt) is { } freezeFailure)
                return Result.Failure(freezeFailure.ToRequestError());

            var freeze = await executor.ExecuteAsync(new TypeIIPeriod(request.TenantId,
                    request.ProgramId, request.PeriodId), aggregate =>
                    CommandFailureRequestAdapter.ToOutcome(aggregate.FreezeClose(
                        request.ExpectedRevision, content, memberId, role, frozenAt)),
                context, ct).ConfigureAwait(false);
            if (!freeze.IsSuccess)
            {
                var latest = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
                    request.ProgramId, request.PeriodId), ct).ConfigureAwait(false);
                existing = latest.Current?.CloseSnapshots.FirstOrDefault(item =>
                    item.CloseSnapshotId == request.CloseSnapshotId);
                if (existing is null || !MatchesFrozenSnapshot(existing, content, memberId, role))
                    return freeze;
            }
        }

        var saved = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.PeriodId), ct).ConfigureAwait(false);
        var snapshot = saved.Current?.CloseSnapshots.FirstOrDefault(item =>
            item.CloseSnapshotId == request.CloseSnapshotId);
        if (snapshot is null || !MatchesFrozenSnapshot(snapshot, content, memberId, role))
            return Conflict("The frozen close snapshot could not be verified after persistence.");

        return Result.Success;
    }

    static bool MatchesFrozenSnapshot(TypeIIPeriodCloseSnapshotView snapshot,
        TypeIIPeriodCloseSnapshotContent content, Uuid memberId, string role) =>
        snapshot.CloseSnapshotId == content.CloseSnapshotId && snapshot.Version == 1 &&
        snapshot.AmendsCloseSnapshotId is null && snapshot.ActorMemberId == memberId &&
        StringComparer.OrdinalIgnoreCase.Equals(snapshot.ActorRole, role) &&
        SameReferences(snapshot.SourceReferences, content.SourceReferences) &&
        snapshot.ExpectedOccurrenceIds.SequenceEqual(content.ExpectedOccurrenceIds) &&
        snapshot.AcknowledgedMaterialItemIds.SequenceEqual(content.AcknowledgedMaterialItemIds);

    static bool SameReferences(IReadOnlyList<ExaminationContentReference> left,
        IReadOnlyList<ExaminationContentReference> right) => left.Count == right.Count &&
        left.SequenceEqual(right);

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
