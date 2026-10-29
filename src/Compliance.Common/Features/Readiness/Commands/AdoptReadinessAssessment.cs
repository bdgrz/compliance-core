using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Adopts the exact computed assessment as the latest of the program. <paramref name="Source" /> is the recorded
///     assessment and <paramref name="RequestProof" /> the admission that reserved <paramref name="AdmissionGeneration" />;
///     the receiver matches the pending generation, the assessment and the immutable computation result. The
///     pre-admission program revision is never compared with the already advanced program.
/// </summary>
[Discriminator("bdgrz.program-readiness.adopt-assessment", 1)]
public sealed record AdoptReadinessAssessment(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ProgramId,
    Uuid AssessmentId, long AdmissionGeneration, NativeFactReference RequestProof)
    : IRequest<ReceiverOutcome>, IReadinessReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
