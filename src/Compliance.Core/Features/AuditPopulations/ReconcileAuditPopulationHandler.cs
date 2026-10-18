using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class ReconcileAuditPopulationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReconcileAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReconcileAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Reconciling an audit population requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.PopulationId), aggregate =>
            {
                if (aggregate.Current is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit population was not found.")));
                var rows = current.Rows;
                var content = new AuditPopulationReconciliationContent(rows.Count,
                    Count(rows, "included"), Count(rows, "excluded"), Count(rows, "duplicate"),
                    Count(rows, "missing"), Count(rows, "rejected"), Count(rows, "unresolved"),
                    Count(rows, "canceled"), Count(rows, "not_applicable"),
                    Count(rows, "exception_backed"), request.Rationale);
                return CommandFailureRequestAdapter.ToOutcome(aggregate.Reconcile(
                    request.ExpectedRevision, content, actorMemberId, actorDisplay,
                    clock.GetUtcNow()));
            }, context, ct);
    }

    static int Count(IReadOnlyList<AuditPopulationRowView> rows, string classification) =>
        rows.Count(row => row.Classification == classification);
}
