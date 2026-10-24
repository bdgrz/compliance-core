using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One reservation of the program calendar, keyed by its root operation. <paramref name="Generation" /> is the
///     calendar's own monotonic counter at the grant (zero for a fence). A fence has no range.
/// </summary>
public sealed record TypeIIRangeReservation(Uuid OperationId, Uuid PeriodId, TypeIIRangeReservationState State,
    TypeIIRangeOperationKind Kind, ObservationRange? Range, long Generation, Uuid? PriorOperationId,
    TypeIIPeriodRangeAuthorized? AuthorizedFact, TypeIIPeriodRangeSettled? SettledFact)
{
    /// <summary>True while the range blocks overlapping ranges of other periods.</summary>
    public bool IsLive => State is TypeIIRangeReservationState.Pending or TypeIIRangeReservationState.Active;
}
