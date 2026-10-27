using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class ReviseAuditPopulationDefinitionHandler(AuditPopulationMutations mutations) : IRequestHandler<ReviseAuditPopulationDefinition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditPopulationDefinition> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
