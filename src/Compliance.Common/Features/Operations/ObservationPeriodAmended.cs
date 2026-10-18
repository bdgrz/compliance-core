using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the ObservationPeriodAmended domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.observation-period-amended", 2)]
public sealed record ObservationPeriodAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt) : DomainEvent;
