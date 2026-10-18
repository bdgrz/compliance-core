using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the ObservationPeriodStarted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.observation-period-started", 2)]
public sealed record ObservationPeriodStarted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt) : DomainEvent;
