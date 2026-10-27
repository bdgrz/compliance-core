using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     The exact gap plan is held for a readiness decision. The use does not advance the gap plan revision:
///     <paramref name="Revision" /> is the held revision, and <paramref name="Generation" /> is the adoption generation
///     the decision seals.
/// </summary>
[Discriminator("bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized", 1)]
public sealed record ReadinessGapPlanReadinessDecisionUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
