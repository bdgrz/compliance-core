using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     What the root-fact reactors of the administrator transition share: read the frozen intent of the guard fact,
///     start the ordered process only when the fact's transition kind belongs to the reactor, and drive it.
/// </summary>
static class AdministratorTransitionReactions
{
    public static async ValueTask StartAsync(ProcessRootReaction process, IImmutableContentStore content,
        IReactorContext context, ITenantManagerGuardFact fact, Func<AdministratorTransitionKind, bool> selects,
        string worker, CancellationToken ct)
    {
        var intent = await content.ReadJsonAsync<AdministratorTransitionIntent>(context.Source.Stream.Realm, fact.Intent, ct)
            .ConfigureAwait(false);
        if (!selects(intent.TransitionKind))
            return;
        await process.StartAsync(AdministratorTransitionProcess.Name, context, fact.Intent, [], worker, ct)
            .ConfigureAwait(false);
    }
}
