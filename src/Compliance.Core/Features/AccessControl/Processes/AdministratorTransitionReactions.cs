using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

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
