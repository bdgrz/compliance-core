using Bdgrz.Compliance.Features.NativeOperations;
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

        var operation = AuthoredCommandReplay.Create(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId, request.CommandId), request.CommandId,
            nameof(ReviseExaminationEngagement), memberId, request.ExpectedRevision, request, clock.GetUtcNow());
        var replayOwner = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId, request.CommandId), ct).ConfigureAwait(false);
        if (AuthoredCommandReplay.Unit(replayOwner.ReplayAcceptance, operation) is { } replay)
            return replay;
        var currentEngagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId, request.CommandId), ct).ConfigureAwait(false);
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
                request.ProgramId, request.ExaminationId, request.CommandId), engagement =>
                CommandFailureRequestAdapter.ToOutcome(engagement.Revise(
                    request.ExpectedRevision, content, memberId, display, clock.GetUtcNow(), operation: operation)),
            context, ct).ConfigureAwait(false);
    }
}
