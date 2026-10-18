using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class FreezeAuditPopulationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<FreezeAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<FreezeAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Freezing an audit population requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.PopulationId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Freeze(request.ExpectedRevision, request.FreezeRationale, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
