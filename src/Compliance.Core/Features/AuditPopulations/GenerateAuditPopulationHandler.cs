using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class GenerateAuditPopulationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<GenerateAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GenerateAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Starting audit population collection requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.DefinitionId), aggregate =>
            {
                if (aggregate.Current is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit population was not found.")));
                if (current.CloseSnapshot.CloseSnapshotId != request.PeriodCloseId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "Collection must target the exact period-close snapshot pinned by the population.")));
                return CommandFailureRequestAdapter.ToOutcome(aggregate.Generate(
                    aggregate.Revision, request.GenerationKey, actorMemberId, actorDisplay,
                    clock.GetUtcNow()));
            }, context, ct);
    }
}
