using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The root native acceptance of a period opening. The complete opening intent is persisted here before the
///     calendar is touched; the period only exists once <see cref="ObservationPeriodStarted" /> follows.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-opening-accepted", 1)]
public sealed record TypeIIPeriodOpeningAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
