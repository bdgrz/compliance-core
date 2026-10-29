using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Computes the assessment of an admitted run on its own owner. The computation reads only the frozen
///     <see cref="ReadinessRunIntent" /> inputs and the rule version they name; it never reads a live source, so a
///     replay or a rerun after a crash produces the identical assessment.
/// </summary>
[Discriminator("bdgrz.readiness-assessment.compute", 1)]
public sealed record ComputeReadinessAssessment(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision,
    ImmutableContentRef Intent, Uuid ProgramId, Uuid AssessmentId)
    : IRequest<ReceiverOutcome>, IReadinessReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
