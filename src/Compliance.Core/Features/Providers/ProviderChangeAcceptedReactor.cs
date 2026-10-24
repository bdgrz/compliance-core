using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Starts the provider name-change process from each accepted provider change.</summary>
public sealed partial class ProviderChangeAcceptedReactor(IProjectionCheckpointStore checkpoints,
    NamedRecordReactions reactions)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(Provider.Area), "ProviderChangeAcceptedV1"),
      IReactorHandler<ProviderChangeAccepted>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IReactorContext<ProviderChangeAccepted> context, CancellationToken ct) =>
        reactions.StartAsync(ProviderNames.Descriptor, context, context.Trigger, "ProviderChangeAcceptedReactor", ct);
}
