using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class GenerateAuditPopulationHandler(AuditPopulationMutations mutations,
    TimeProvider clock) : IRequestHandler<GenerateAuditPopulation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GenerateAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Starting audit population collection requires a personal HTTP actor."));

        return await mutations.GenerateAsync(context, actorMemberId, actorDisplay, clock, ct).ConfigureAwait(false);
    }
}
