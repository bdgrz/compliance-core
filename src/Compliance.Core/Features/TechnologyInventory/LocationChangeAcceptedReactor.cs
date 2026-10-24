using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Starts the location name-change process from each accepted location change.</summary>
public sealed partial class LocationChangeAcceptedReactor(IProjectionCheckpointStore checkpoints,
    NamedRecordReactions reactions)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(InventoryRegisters.Area), "LocationChangeAcceptedV1"),
      IReactorHandler<LocationChangeAccepted>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IReactorContext<LocationChangeAccepted> context, CancellationToken ct) =>
        reactions.StartAsync(LocationNames.Descriptor, context, context.Trigger, "LocationChangeAcceptedReactor", ct);
}
