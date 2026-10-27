using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Approves the independently reviewed Type II plan under current CEO/CISO authority.</summary>
public sealed class ApproveTypeIIOperatingPlanHandler(IAggregateExecutor executor,
    ManagementExecutiveAuthority executiveAuthority, TimeProvider clock)
    : IRequestHandler<ApproveTypeIIOperatingPlan>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveTypeIIOperatingPlan> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II plan approval requires a personal HTTP actor."));
        if (request.Version is <= 0 or > int.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type II plan approval must identify a positive plan version."));

        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, ct).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II plan approval requires one current program-scoped CEO or CISO permission."));

        return await executor.ExecuteAsync(new TypeIIPlan(request.TenantId, request.ProgramId,
                request.PlanId), plan => CommandFailureRequestAdapter.ToOutcome(plan.Approve(
                plan.Revision, (int)request.Version, request.Rationale, memberId, role, display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
