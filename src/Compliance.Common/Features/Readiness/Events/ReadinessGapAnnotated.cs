using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     Advisor feedback was recorded on a gap as the root native fact of its operation. <c>Revision</c> is the gap plan
///     revision at the annotation, which an annotation never advances.
/// </summary>
[Discriminator("bdgrz.readiness.gap.annotated", 1)]
public sealed record ReadinessGapAnnotated(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef AnnotationContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
