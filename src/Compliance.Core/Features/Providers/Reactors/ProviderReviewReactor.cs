using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.Providers.Reactors;

/// <summary>
///     Starts <c>ProviderReviewProcess</c> from a committed review admission. The process record, with its frozen
///     intent, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ProviderReviewReactor(IProjectionCheckpointStore checkpoints, global::Bdgrz.Compliance.Features.Processes.Processes.ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProviderAssuranceStreams.Area), WorkloadName),
        IReactorHandler<ProviderReviewRequested>
{
    public const string WorkloadName = "ProviderReviewV1";

    public async ValueTask HandleAsync(IReactorContext<ProviderReviewRequested> context, CancellationToken ct) =>
        _ = await process.StartAsync(ProviderReviewEffects.ProcessName, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}
