using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Completes a reserved date change after the Type II period stream accepted it.</summary>
[Discriminator("bdgrz.operations.observation-period-amendment-completed", 1)]
public sealed record ObservationPeriodAmendmentCompleted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid ActorMemberId,
    Uuid PeriodId,
    TypeIIObservationCalendarAmendmentView Amendment,
    DateTimeOffset OccurredAt) : DomainEvent;
