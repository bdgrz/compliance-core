using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class RecordAuditorRequestHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<RecordAuditorRequest>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordAuditorRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording an auditor request requires a personal HTTP actor."));
        if (!await AuditorRequestOwner.IsActiveClientMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result.Failure(AuditorRequestOwner.InvalidOwner());

        var content = new AuditorRequestContent(request.ExaminationId, request.Source,
            request.ExternalReference, request.Wording, request.Scope, request.Priority,
            request.OwnerMemberId, request.DueOn, true);
        return await executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Create(content, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
