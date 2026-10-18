using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class ReviseExaminationEngagementHandler(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseExaminationEngagement>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseExaminationEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An examination engagement decision requires a personal HTTP actor."));

        var currentEngagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId), ct).ConfigureAwait(false);
        if (currentEngagement.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The examination engagement was not found."));
        var contacts = request.ManagementContactMemberIds ?? current.ManagementContactMemberIds;
        var validation = await ExaminationManagementContacts.ValidateAsync(reader,
            request.TenantId, contacts, ct).ConfigureAwait(false);
        if (validation is not null)
            return Result.Failure(validation);
        var content = new ExaminationEngagementContent(current.EngagementType,
            request.AuditFirm, request.PeriodStart, request.PeriodEnd,
            request.TrustServicesCategories, current.BoundaryId, contacts);
        return await executor.ExecuteAsync(new ExaminationEngagement(request.TenantId,
                request.ProgramId, request.ExaminationId), engagement =>
                CommandFailureRequestAdapter.ToOutcome(engagement.Revise(
                    request.ExpectedRevision, content, memberId, display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
