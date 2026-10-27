using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class FreezeAuditPopulationHandler(AuditPopulationMutations mutations) : IRequestHandler<FreezeAuditPopulation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<FreezeAuditPopulation> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
