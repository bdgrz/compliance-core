using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Quality-management partners govern mutable drafts and immutable ratified versions.</summary>
sealed class ReviseIndependenceRuleSetDraftHandler(IAggregateExecutor executor,
    FirmServicesAuthority authority, TimeProvider clock) : IRequestHandler<ReviseIndependenceRuleSetDraft, IndependenceRuleSetView>
{
    public async ValueTask<Result<IndependenceRuleSetView>> HandleAsync(IRequestContext<ReviseIndependenceRuleSetDraft> context,
        CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "quality_management_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<IndependenceRuleSetView>.Failure(actor.Error!);
        var request = context.Request;
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new IndependenceRuleSet(request.Version, request.CommandId), owner =>
        {
            var result = owner.Revise(request, actor.Value, at);
            return result.IsSuccess ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);
        }, context, ct).ConfigureAwait(false);
    }
}
