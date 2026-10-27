using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Freezes the exact approved close and complete occurrence coverage in one native period admission.</summary>
public sealed class FreezeTypeIIPeriodCloseHandler(TypeIIPeriodOperations operations,
    ManagementExecutiveAuthority executiveAuthority, TypeIIOccurrenceCoverage coverage)
    : IRequestHandler<FreezeTypeIIPeriodClose, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<FreezeTypeIIPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out _) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Freezing a close requires a personal HTTP actor."));
        if (request.CloseSnapshotId == Uuid.Empty)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation, "A close freeze requires its exact immutable snapshot identity."));
        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId, request.ProgramId, user, member, ct).ConfigureAwait(false);
        if (role is null)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Freezing a close requires current program-scoped CEO or CISO authority."));
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, request.CommandId,
            nameof(FreezeTypeIIPeriodClose), request.ExpectedRevision, async (snapshot, operation, token) =>
            {
                var current = snapshot.View!;
                var approval = current.CloseApprovals.Count == 0 ? null : current.CloseApprovals[^1];
                if (current.CloseValidation is not { Status: "ready" } validation || approval is null)
                    return Conflict("The exact ready reconciliation and current executive approval are required.");
                if (approval.ApprovedByMemberId != member || !StringComparer.OrdinalIgnoreCase.Equals(approval.ApproverRole, role))
                    return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only the same currently authorized executive can freeze this exact approval."));
                var proof = await coverage.ProveAsync(snapshot, token).ConfigureAwait(false);
                if (!proof.IsComplete)
                    return Conflict(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Closing the Type II period requires complete occurrence coverage: {proof.NotMaterialized.Count} selected occurrences are not materialized and {proof.NotSelected.Count} expected occurrences are not selected."));
                var body = new TypeIIPeriodCloseSnapshotContent(request.CloseSnapshotId, validation.SourceReferences,
                    validation.ExpectedOccurrenceIds, approval.AcknowledgedMaterialItemIds)
                { SourceContentManifest = validation.SourceContentManifest ?? current.OpeningSourceContentManifest };
                return TypeIIPeriodOperations.Outcome(snapshot.Rules.FreezeClose(request.ExpectedRevision, body, member, role, operation.AcceptedAt));
            }, ct).ConfigureAwait(false);
    }
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
