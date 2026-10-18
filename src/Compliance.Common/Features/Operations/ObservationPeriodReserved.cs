using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reserves one program date range for a Type II observation period.</summary>
[Discriminator("bdgrz.operations.observation-period-reserved", 1)]
public sealed record ObservationPeriodReserved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIObservationCalendarPeriodView Period,
    DateTimeOffset OccurredAt) : DomainEvent;
