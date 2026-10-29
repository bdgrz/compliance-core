using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     Advisor feedback was recorded on a gap as the root native fact of its operation. <c>Revision</c> is the gap plan
///     revision at the annotation, which an annotation never advances.
/// </summary>
[Discriminator("bdgrz.readiness.gap.annotated", 1)]
public sealed record ReadinessGapAnnotated(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef AnnotationContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
