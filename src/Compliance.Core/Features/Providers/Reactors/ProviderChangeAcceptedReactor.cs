using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.Providers.Reactors;

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
