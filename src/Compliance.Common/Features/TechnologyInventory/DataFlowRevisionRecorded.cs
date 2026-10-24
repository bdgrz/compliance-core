using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Records one complete, attributed data-flow version; revision 1 creates it. The fact accepts its own logical operation, so an exact retry of the
///     creation or revision replays the frozen original response however far the record has moved since.
/// </summary>
[Discriminator("bdgrz.inventory.data_flow.revision_recorded", 1)]
public sealed record DataFlowRevisionRecorded(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, ActorReference Actor, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
