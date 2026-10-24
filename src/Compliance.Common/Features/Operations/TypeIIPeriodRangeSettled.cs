using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The calendar settled exactly one reservation, <paramref name="TargetOperationId" />, and for an applied
///     amendment also released the replaced <paramref name="ReleasedOperationId" />. A settlement before any
///     authorization fences the reservation so a late acquisition is refused.
/// </summary>
[Discriminator("bdgrz.type-ii-observation-calendar.type-ii-period-range-settled", 1)]
public sealed record TypeIIPeriodRangeSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid PeriodId,
    TypeIIRangeSettlementKind Settlement, Uuid TargetOperationId, Uuid? ReleasedOperationId)
    : DomainEvent, INativeOperationFact;
