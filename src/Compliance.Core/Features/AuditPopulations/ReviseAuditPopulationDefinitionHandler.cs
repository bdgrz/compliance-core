using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class ReviseAuditPopulationDefinitionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseAuditPopulationDefinition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditPopulationDefinition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising an audit population definition requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.DefinitionId), aggregate =>
            {
                if (aggregate.Current?.CurrentDefinition is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit population definition was not found.")));
                var content = current.Content with
                {
                    SourceUniverse = request.SourceBoundary,
                    InclusionRules = request.InclusionRules,
                    ExclusionRules = request.ExclusionRules,
                };
                return CommandFailureRequestAdapter.ToOutcome(aggregate.ReviseDefinition(
                    request.ExpectedRevision, content, request.ChangeReason, actorMemberId,
                    actorDisplay, clock.GetUtcNow()));
            }, context, ct);
    }
}
