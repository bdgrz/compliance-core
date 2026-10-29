using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class ReviseAuditPopulationDefinitionHandler(AuditPopulationMutations mutations) : IRequestHandler<ReviseAuditPopulationDefinition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditPopulationDefinition> context, CancellationToken ct) =>
        mutations.ExecuteAsync(context, ct);
}
