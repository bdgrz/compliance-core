using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
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
