using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Queries;
using Bdgrz.Compliance.Features.FirmServices.Readers;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Returns current authoritative policy without reauthorizing its historical ratifier.</summary>
sealed class GetActiveIndependenceRuleSetHandler(IndependenceRuleSetReader reader, FirmServicesAuthority authority)
    : IRequestHandler<GetActiveIndependenceRuleSet, IndependenceRuleSetView>
{
    public async ValueTask<Result<IndependenceRuleSetView>> HandleAsync(
        IRequestContext<GetActiveIndependenceRuleSet> context, CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "engagement_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<IndependenceRuleSetView>.Failure(actor.Error!);
        var active = await reader.ReadActiveAsync(ct).ConfigureAwait(false);
        return active is null
            ? Result<IndependenceRuleSetView>.Failure(new(RequestErrorKind.NotFound, "No rule-set version has been ratified."))
            : Result<IndependenceRuleSetView>.Success(active);
    }
}
