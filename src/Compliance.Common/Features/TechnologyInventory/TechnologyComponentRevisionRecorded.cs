using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Records one complete, attributed component revision; revision 1 creates it. The fact accepts its own logical operation, so an exact retry of the
///     creation or revision replays the frozen original response however far the record has moved since.
/// </summary>
[Discriminator("bdgrz.inventory.component.revision_recorded", 1)]
public sealed record TechnologyComponentRevisionRecorded(Uuid TenantId, Uuid ComponentId, long Revision,
    TechnologyComponentContent Content, ActorReference Actor, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
