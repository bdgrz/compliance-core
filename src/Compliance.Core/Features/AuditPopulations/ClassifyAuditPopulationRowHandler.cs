using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class ClassifyAuditPopulationRowHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ClassifyAuditPopulationRow>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ClassifyAuditPopulationRow> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Classifying an audit population row requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.PopulationId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.ClassifyRow(aggregate.Revision, request.RowId, request.Classification,
                    request.Rationale, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
