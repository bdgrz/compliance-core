using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     A readiness decision use of a gap plan ended: released after the irreversible decision, or cleared after the
///     decision was canceled. A settlement before any grant fences the key so a delayed acquisition cannot recreate the
///     authority. The disposition is recorded in the settlement intent.
/// </summary>
[Discriminator("bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-settled", 1)]
public sealed record ReadinessGapPlanReadinessDecisionUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
