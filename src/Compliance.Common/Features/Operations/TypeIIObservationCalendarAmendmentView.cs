using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One exact date change reserved while its Type II period stream is updated.</summary>
public sealed record TypeIIObservationCalendarAmendmentView(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Reason,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);
