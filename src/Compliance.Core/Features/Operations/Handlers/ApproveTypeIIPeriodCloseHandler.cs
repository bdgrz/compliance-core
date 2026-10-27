using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Native close approval over an exact ready reconciliation, with current executive authorization before replay.</summary>
public sealed class ApproveTypeIIPeriodCloseHandler(TypeIIPeriodOperations operations,
    ManagementExecutiveAuthority executiveAuthority) : IRequestHandler<ApproveTypeIIPeriodClose, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<ApproveTypeIIPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Close approval requires a personal HTTP actor."));
        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId, request.ProgramId, user, member, ct).ConfigureAwait(false);
        if (role is null)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Close approval requires current program-scoped CEO or CISO authority."));
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, request.CommandId,
            nameof(ApproveTypeIIPeriodClose), request.ExpectedRevision, (snapshot, operation, _) => ValueTask.FromResult(
                TypeIIPeriodOperations.Outcome(snapshot.Rules.ApproveClose(request.ExpectedRevision, request.AcknowledgedMaterialItemIds,
                    request.Rationale, member, role, display, operation.AcceptedAt))), ct).ConfigureAwait(false);
    }
}
