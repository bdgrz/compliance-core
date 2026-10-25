using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Immutable scalar reservation authority; actual source facts are resolved from their native revisions.</summary>
public sealed record TypeIICalendarReservationContent(Uuid OperationId, Uuid PeriodId, TypeIIRangeReservationState State,
    TypeIIRangeOperationKind Kind, ObservationRange? Range, long Generation, Uuid? PriorOperationId,
    long? AuthorizationRevision, long? SettlementRevision)
{
    public string? AuthorizationBindingSha256 { get; init; }
    public string? SettlementBindingSha256 { get; init; }
    public bool IsLive => State is TypeIIRangeReservationState.Pending or TypeIIRangeReservationState.Active;
}
