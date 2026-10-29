using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class AmendAuditPopulationHandler(AuditPopulationMutations mutations) : IRequestHandler<AmendAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AmendAuditPopulation> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
