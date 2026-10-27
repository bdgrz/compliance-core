using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The root native acceptance of a readiness run. It reserves the one pending latest-adoption generation
///     <c>Generation</c> and references the frozen <see cref="ReadinessRunIntent" /> with its complete input snapshot;
///     the assessment is only the latest once <see cref="ReadinessAssessmentAdopted" /> follows.
/// </summary>
[Discriminator("bdgrz.program-readiness.readiness-assessment-requested", 1)]
public sealed record ReadinessAssessmentRequested(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
