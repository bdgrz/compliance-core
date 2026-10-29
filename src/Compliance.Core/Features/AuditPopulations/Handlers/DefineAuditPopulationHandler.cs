using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class DefineAuditPopulationHandler(AuditPopulationMutations mutations,
    TypeIIPeriodReader periods, TimeProvider clock) : IRequestHandler<DefineAuditPopulation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DefineAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Defining an audit population requires a personal HTTP actor."));

        return await mutations.DefineAsync(context, actorMemberId, actorDisplay, periods, clock, ct).ConfigureAwait(false);
    }
}
