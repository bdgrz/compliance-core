using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>An attributable effective-date change preserving the approved plan and opening snapshot.</summary>
public sealed record TypeIIPeriodAmendmentView(
    DateOnly PriorPeriodStart,
    DateOnly PriorPeriodEnd,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Reason,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset AmendedAt);
