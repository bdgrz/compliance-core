using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
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
