using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Reactors;

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
