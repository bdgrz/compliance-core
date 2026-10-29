using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     The calendar settled exactly one reservation, <paramref name="TargetOperationId" />, and for an applied
///     amendment also released the replaced <paramref name="ReleasedOperationId" />. A settlement before any
///     authorization fences the reservation so a late acquisition is refused.
/// </summary>
[Discriminator("bdgrz.type-ii-observation-calendar.type-ii-period-range-settled", 2)]
public sealed record TypeIIPeriodRangeSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid PeriodId,
    TypeIIRangeSettlementKind Settlement, Uuid TargetOperationId, Uuid? ReleasedOperationId,
    TypeIIObservationCalendarHeader Header, ImmutableContentRef CurrentAuthority)
    : DomainEvent, INativeOperationFact;
