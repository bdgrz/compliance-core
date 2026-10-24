using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     A pending amendment ended before it applied, by explicit cancellation or by a proven calendar conflict. The
///     fact is the local receiver fence: a delayed apply sees it and does nothing, and only then is the pending
///     calendar reservation released.
/// </summary>
[Discriminator("bdgrz.type-ii-period.type-ii-period-amendment-canceled", 1)]
public sealed record TypeIIPeriodAmendmentCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid ProgramId) : DomainEvent, INativeOperationFact;
