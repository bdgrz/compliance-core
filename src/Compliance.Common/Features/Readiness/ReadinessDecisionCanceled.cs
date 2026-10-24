using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     A pending decision ended before it was recorded. The fact is the local receiver fence: a delayed completion sees
///     it and does nothing, and only then are the gap plan uses settled. <c>SourceProofs</c> is the failure proof.
/// </summary>
[Discriminator("bdgrz.program-readiness.readiness-decision-canceled", 1)]
public sealed record ReadinessDecisionCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
