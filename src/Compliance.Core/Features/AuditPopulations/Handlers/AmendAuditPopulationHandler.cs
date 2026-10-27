using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class AmendAuditPopulationHandler(AuditPopulationMutations mutations) : IRequestHandler<AmendAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AmendAuditPopulation> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
