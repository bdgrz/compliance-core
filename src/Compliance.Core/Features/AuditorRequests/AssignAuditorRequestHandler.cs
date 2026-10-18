using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class AssignAuditorRequestHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<AssignAuditorRequest>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignAuditorRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Assigning an auditor request requires a personal HTTP actor."));
        if (!await AuditorRequestOwner.IsActiveClientMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result.Failure(AuditorRequestOwner.InvalidOwner());

        return await executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Assign(aggregate.Revision, request.OwnerMemberId, request.DueOn,
                    request.AssignmentReason, actorMemberId, actorDisplay, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
