using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class ClassifyAuditPopulationRowHandler(AuditPopulationMutations mutations) : IRequestHandler<ClassifyAuditPopulationRow>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ClassifyAuditPopulationRow> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
