using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Runs the manual readiness rules for a program as of a time (now by default). <paramref name="AssessmentId" /> is
///     the logical operation identity and the assessment identity. The run is admitted on the program readiness owner at
///     <paramref name="ExpectedProgramRevision" />, freezes its complete inputs and as-of time once, and is acknowledged
///     with 202; the assessment is then computed from the frozen inputs on its own owner and adopted as the latest. It
///     is HTTP-only and never an audit opinion.
/// </summary>
[Discriminator("bdgrz.readiness.assessment.run", 2)]
public sealed record RunReadinessAssessment(Uuid AssessmentId, Uuid TenantId, Uuid ProgramId,
    long ExpectedProgramRevision, DateTimeOffset? AsOf = null)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => AssessmentId;
}
