using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Starts <c>ProviderReviewProcess</c> from a committed review admission. The process record, with its frozen
///     intent, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ProviderReviewReactor(IProjectionCheckpointStore checkpoints, Processes.ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ProviderAssuranceStreams.Area), WorkloadName),
        IReactorHandler<ProviderReviewRequested>
{
    public const string WorkloadName = "ProviderReviewV1";

    public async ValueTask HandleAsync(IReactorContext<ProviderReviewRequested> context, CancellationToken ct) =>
        _ = await process.StartAsync(ProviderReviewEffects.ProcessName, context, context.Trigger.Intent, [],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}
