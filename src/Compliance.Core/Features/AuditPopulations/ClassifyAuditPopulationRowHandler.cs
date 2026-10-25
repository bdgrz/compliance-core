using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class ClassifyAuditPopulationRowHandler(AuditPopulationMutations mutations) : IRequestHandler<ClassifyAuditPopulationRow>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ClassifyAuditPopulationRow> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
