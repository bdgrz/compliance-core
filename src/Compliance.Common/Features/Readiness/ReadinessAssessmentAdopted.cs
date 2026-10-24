using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The exact computed assessment became the latest of the program. <c>Generation</c> is the pending adoption
///     generation the run reserved; <c>Operation.RootOperationId</c> is the assessment identity. <c>SourceProofs</c>
///     holds the request and recorded-assessment proofs, and <c>OperationResult</c> the original registration with the
///     program revision after this adoption.
/// </summary>
[Discriminator("bdgrz.program-readiness.readiness-assessment-adopted", 1)]
public sealed record ReadinessAssessmentAdopted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
