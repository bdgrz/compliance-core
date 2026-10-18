using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Approves an exact reconciled period close under current CEO/CISO authority.</summary>
public sealed class ApproveTypeIIPeriodCloseHandler(IAggregateExecutor executor,
    ManagementExecutiveAuthority executiveAuthority, TimeProvider clock)
    : IRequestHandler<ApproveTypeIIPeriodClose>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveTypeIIPeriodClose> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II period close approval requires a personal HTTP actor."));
        if (request.ExpectedRevision <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type II close approval must identify a positive period revision."));

        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, ct).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II period close approval requires one current program-scoped CEO or CISO permission."));

        return await executor.ExecuteAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
                request.PeriodId), period =>
                CommandFailureRequestAdapter.ToOutcome(period.ApproveClose(request.ExpectedRevision,
                    request.AcknowledgedMaterialItemIds, request.Rationale, memberId, role, display,
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
