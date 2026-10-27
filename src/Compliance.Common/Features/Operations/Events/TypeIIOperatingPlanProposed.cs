using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>Records the TypeIIOperatingPlanProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.type-i-i-operating-plan-proposed", 1)]
public sealed record TypeIIOperatingPlanProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
