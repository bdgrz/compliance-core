using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Protects a proposed replacement date range while its period amendment is committed.</summary>
[Discriminator("bdgrz.operations.observation-period-amendment-reserved", 1)]
public sealed record ObservationPeriodAmendmentReserved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid ActorMemberId,
    Uuid PeriodId,
    TypeIIObservationCalendarAmendmentView Amendment,
    DateTimeOffset OccurredAt) : DomainEvent;
