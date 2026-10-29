using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The temporary reservation of a practice subject ended after the operation recorded its terminal fact. A
///     settlement before any reservation fences the key against late acquisition. A permanent commitment made by the
///     operation is never removed by its settlement.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.practice-independence-reservation-settled", 1)]
public sealed record PracticeIndependenceReservationSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
