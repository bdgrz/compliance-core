using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Quality-management partners govern mutable drafts and immutable ratified versions.</summary>
sealed class DraftIndependenceRuleSetHandler(IAggregateReader reader, IAggregateExecutor executor,
    FirmServicesAuthority authority, TimeProvider clock) : IRequestHandler<DraftIndependenceRuleSet, IndependenceRuleSetView>
{
    public async ValueTask<Result<IndependenceRuleSetView>> HandleAsync(IRequestContext<DraftIndependenceRuleSet> context,
        CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "quality_management_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<IndependenceRuleSetView>.Failure(actor.Error!);
        var request = context.Request;
        IndependenceRuleSetView? predecessor = null;
        if (request.Version > 1)
            predecessor = (await reader.HydrateAsync(new IndependenceRuleSet(request.Version - 1), ct)
                .ConfigureAwait(false)).View;
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new IndependenceRuleSet(request.Version, request.CommandId), owner =>
        {
            var result = owner.Draft(request, predecessor, actor.Value, at);
            return result.IsSuccess ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);
        }, context, ct).ConfigureAwait(false);
    }
}
