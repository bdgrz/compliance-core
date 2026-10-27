using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Readiness.Events;

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
