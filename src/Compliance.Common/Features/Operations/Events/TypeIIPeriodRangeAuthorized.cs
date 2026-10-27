using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The calendar holds the new range of one opening or amendment. The calendar's own reservation generation is the
///     number of authorizations up to and including this fact. <paramref name="PriorOperationId" /> names the live
///     reservation an amendment replaces; that reservation stays held until the amendment is applied. The range and
///     period remain on the native fact; Header and CurrentAuthority bind the compact live authority and linked terminal history.
/// </summary>
[Discriminator("bdgrz.type-ii-observation-calendar.type-ii-period-range-authorized", 2)]
public sealed record TypeIIPeriodRangeAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid PeriodId,
    TypeIIRangeOperationKind Kind, ObservationRange NewRange, Uuid? PriorOperationId,
    TypeIIObservationCalendarHeader Header, ImmutableContentRef CurrentAuthority)
    : DomainEvent, INativeOperationFact;
