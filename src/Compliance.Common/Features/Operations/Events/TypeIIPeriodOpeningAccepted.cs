using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     The root native acceptance of a period opening. The complete opening intent is persisted here before the
///     calendar is touched; the period only exists once <see cref="ObservationPeriodStarted" /> follows.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-opening-accepted", 1)]
public sealed record TypeIIPeriodOpeningAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId) : DomainEvent, INativeOperationFact;
