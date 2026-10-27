using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The exact gap plan is held for a readiness decision. The use does not advance the gap plan revision:
///     <paramref name="Revision" /> is the held revision, and <paramref name="Generation" /> is the adoption generation
///     the decision seals.
/// </summary>
[Discriminator("bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized", 1)]
public sealed record ReadinessGapPlanReadinessDecisionUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
