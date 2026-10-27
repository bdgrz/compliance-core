using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class ReconcileAuditPopulationHandler(AuditPopulationMutations mutations) : IRequestHandler<ReconcileAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReconcileAuditPopulation> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
