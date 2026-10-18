using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class ReviseExaminationEngagementHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseExaminationEngagement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseExaminationEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An examination engagement decision requires a personal HTTP actor.")));
        return executor.ExecuteAsync(new ExaminationEngagement(request.TenantId, request.ProgramId,
                request.ExaminationId), engagement =>
            {
                if (engagement.Current is not { } current)
                    return CommandFailureRequestAdapter.ToOutcome(
                        CommandFailure.MissingRecord("The examination engagement was not found."));
                var content = new ExaminationEngagementContent(current.EngagementType,
                    request.AuditFirm, request.PeriodStart, request.PeriodEnd,
                    request.TrustServicesCategories, current.BoundaryId,
                    current.ManagementContactMemberIds);
                return CommandFailureRequestAdapter.ToOutcome(engagement.Revise(
                    request.ExpectedRevision, content, memberId, display, clock.GetUtcNow()));
            }, context, ct);
    }
}
