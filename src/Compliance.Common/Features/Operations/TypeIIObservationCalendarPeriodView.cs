using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One program-wide observation-period reservation and its exact opening snapshot.</summary>
public sealed record TypeIIObservationCalendarPeriodView(
    Uuid PeriodId,
    Uuid ExaminationId,
    Uuid PlanId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string OpeningSnapshotSha256,
    Uuid ReservedByMemberId,
    string ReservedByDisplay,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsActive = true,
    TypeIIObservationCalendarAmendmentView? PendingAmendment = null,
    TypeIIObservationCalendarAmendmentView? LastAmendment = null);
