using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Management's personal readiness decision on the latest assessment: proceed or do_not_proceed. HTTP-only; the
///     member who ran the assessment cannot decide it without an exact separation-of-duties waiver proof. The decision
///     seals the latest adoption and the gap plans, is acknowledged with 202 and is recorded by
///     <c>ReadinessDecisionProcess</c> once every gap plan is held.
/// </summary>
[Discriminator("bdgrz.readiness.decide", 2)]
public sealed record DecideReadiness(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
