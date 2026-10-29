using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class RecordAuditPopulationFactsHandler(AuditPopulationMutations mutations) : IRequestHandler<RecordAuditPopulationFacts>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditPopulationFacts> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
