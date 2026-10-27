using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     A pending opening was rejected because the calendar proved its range conflicts. The rejection fences a delayed
///     apply; the same period identity is never reopened with changed intent.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-opening-rejected", 1)]
public sealed record TypeIIPeriodOpeningRejected(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId) : DomainEvent, INativeOperationFact;
