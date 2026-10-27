using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Reactors;

/// <summary>Starts the operational process name-change process from each accepted operational process change.</summary>
public sealed partial class OperationalProcessChangeAcceptedReactor(IProjectionCheckpointStore checkpoints,
    NamedRecordReactions reactions)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(InventoryRegisters.Area), "OperationalProcessChangeAcceptedV1"),
      IReactorHandler<OperationalProcessChangeAccepted>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IReactorContext<OperationalProcessChangeAccepted> context, CancellationToken ct) =>
        reactions.StartAsync(OperationalProcessNames.Descriptor, context, context.Trigger, "OperationalProcessChangeAcceptedReactor", ct);
}
