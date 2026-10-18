using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Program-wide date reservations preventing contradictory Type II observation periods.</summary>
public sealed record TypeIIObservationCalendarView(
    Uuid TenantId,
    Uuid ProgramId,
    long Revision,
    IReadOnlyList<TypeIIObservationCalendarPeriodView> Periods,
    DateTimeOffset UpdatedAt);
