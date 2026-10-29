using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>Records the TypeIIOperatingPlanApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.type-i-i-operating-plan-approved", 1)]
public sealed record TypeIIOperatingPlanApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
