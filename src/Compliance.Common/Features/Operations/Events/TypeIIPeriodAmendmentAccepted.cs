using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     The root native acceptance of a period amendment. Both the old and the new range are frozen in the intent; the
///     range of the period only changes once <see cref="ObservationPeriodAmended" /> follows.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-amendment-accepted", 1)]
public sealed record TypeIIPeriodAmendmentAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId) : DomainEvent, INativeOperationFact;
