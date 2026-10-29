using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Starts <c>EngagementPersonalDecisionProcess</c> from a committed personal decision admission. The process
///     record, with its frozen intent, exists before the source checkpoint advances; the sweep resumes it if this
///     reactor stops.
/// </summary>
sealed partial class EngagementPersonalDecisionReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, ISourceFactResolver sources, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ServiceEngagementIndependence.Area), WorkloadName),
        IReactorHandler<EngagementPersonalDecisionAdmitted>
{
    public const string WorkloadName = "EngagementPersonalDecisionV1";

    public async ValueTask HandleAsync(IReactorContext<EngagementPersonalDecisionAdmitted> context,
        CancellationToken ct)
    {
        await EngagementNativeSource.VerifyAsync(sources, content, context.Source.Stream,
            context.Source.ResourceOffset, context.Trigger, ct).ConfigureAwait(false);
        _ = await process.StartAsync(EngagementPersonalDecisionEffects.ProcessName, context,
            context.Trigger.Intent, [], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
