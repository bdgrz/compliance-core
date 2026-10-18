using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class CreateExaminationEngagementHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CreateExaminationEngagement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateExaminationEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An examination engagement requires a personal HTTP actor.")));
        var content = new ExaminationEngagementContent(request.EngagementType, request.AuditFirm,
            request.PeriodStart, request.PeriodEnd, request.TrustServicesCategories,
            request.BoundaryId, []);
        return executor.ExecuteAsync(new ExaminationEngagement(request.TenantId, request.ProgramId,
                request.ExaminationId), engagement => CommandFailureRequestAdapter.ToOutcome(
                engagement.Create(content, memberId, display, clock.GetUtcNow())), context, ct);
    }
}
