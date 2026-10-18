using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class AmendAuditPopulationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<AmendAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AmendAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Amending an audit population requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.PopulationId), aggregate =>
            {
                if (aggregate.Current is null)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit population was not found.")));
                return CommandFailureRequestAdapter.ToOutcome(aggregate.Amend(
                    request.ExpectedRevision, request.AmendmentReason, request.SourceSnapshot,
                    request.ChangedRows, actorMemberId, actorDisplay, clock.GetUtcNow()));
            }, context, ct);
    }
}
