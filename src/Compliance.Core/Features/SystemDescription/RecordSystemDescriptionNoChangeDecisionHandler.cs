using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class RecordSystemDescriptionNoChangeDecisionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordSystemDescriptionNoChangeDecision>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<RecordSystemDescriptionNoChangeDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "A system description period-change decision requires a Bdgrz member identity.")));
        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.RecordNoChangeDecision(request.Version, request.ChangeId, request.Rationale,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
