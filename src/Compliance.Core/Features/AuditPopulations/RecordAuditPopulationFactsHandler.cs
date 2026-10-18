using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class RecordAuditPopulationFactsHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordAuditPopulationFacts>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditPopulationFacts> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording audit population facts requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.DefinitionId), aggregate =>
            {
                if (aggregate.Current is null)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit population definition was not found.")));
                return CommandFailureRequestAdapter.ToOutcome(aggregate.RecordFacts(
                    request.ExpectedRevision, request.SourceSnapshot, request.Rows, actorMemberId,
                    actorDisplay, clock.GetUtcNow()));
            }, context, ct);
    }
}
