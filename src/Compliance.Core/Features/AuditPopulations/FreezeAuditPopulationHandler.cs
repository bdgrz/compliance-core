using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class FreezeAuditPopulationHandler(AuditPopulationMutations mutations) : IRequestHandler<FreezeAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<FreezeAuditPopulation> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
