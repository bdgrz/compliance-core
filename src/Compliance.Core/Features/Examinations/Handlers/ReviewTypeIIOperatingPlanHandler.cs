using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

public sealed class ReviewTypeIIOperatingPlanHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviewTypeIIOperatingPlan>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewTypeIIOperatingPlan> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation,
                request.TenantId, out var reviewerMemberId, out var reviewerDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II plan review requires personal HTTP submission.")));
        if (request.Version is < 1 or > int.MaxValue)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type II plan review must identify a positive plan version.")));

        return executor.ExecuteAsync(new TypeIIPlan(request.TenantId, request.ProgramId,
                request.PlanId), plan => CommandFailureRequestAdapter.ToOutcome(plan.Review(
                plan.Revision, (int)request.Version, request.Outcome, request.Comments,
                reviewerMemberId, reviewerDisplay, clock.GetUtcNow())), context, ct);
    }
}
