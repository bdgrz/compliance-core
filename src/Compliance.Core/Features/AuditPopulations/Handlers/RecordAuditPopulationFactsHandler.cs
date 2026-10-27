using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class RecordAuditPopulationFactsHandler(AuditPopulationMutations mutations) : IRequestHandler<RecordAuditPopulationFacts>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditPopulationFacts> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
