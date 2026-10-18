using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Releases a proposed date change after its Type II period update was rejected.</summary>
[Discriminator("bdgrz.operations.observation-period-amendment-cancelled", 1)]
public sealed record ObservationPeriodAmendmentCancelled(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid ActorMemberId,
    Uuid PeriodId,
    TypeIIObservationCalendarAmendmentView Amendment,
    DateTimeOffset OccurredAt) : DomainEvent;
