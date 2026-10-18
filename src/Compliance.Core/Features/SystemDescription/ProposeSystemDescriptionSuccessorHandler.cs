using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class ProposeSystemDescriptionSuccessorHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ProposeSystemDescriptionSuccessor>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeSystemDescriptionSuccessor> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Proposing a system description successor requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate =>
            {
                if (aggregate.CurrentVersion is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The system description was not found.")));
                return CommandFailureRequestAdapter.ToOutcome(aggregate.ProposeSuccessor(
                    request.ExpectedRevision, context.RequestId, current.Content,
                    request.ChangeReason, actorMemberId, actorDisplay, clock.GetUtcNow()));
            }, context, ct);
    }
}
