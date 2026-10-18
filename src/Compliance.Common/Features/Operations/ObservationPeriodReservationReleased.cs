using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Releases an observation-period date range after its close snapshot is frozen.</summary>
[Discriminator("bdgrz.operations.observation-period-reservation-released", 1)]
public sealed record ObservationPeriodReservationReleased(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid ActorMemberId,
    Uuid PeriodId,
    Uuid CloseSnapshotId,
    string CloseSnapshotSha256,
    DateTimeOffset OccurredAt) : DomainEvent;
