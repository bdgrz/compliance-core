using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     The acting member's personal Type I entry sign-off on the latest readiness assessment: approve,
///     approve_with_exceptions, or defer. Every unresolved gap must be explicitly acknowledged to approve with
///     exceptions. HTTP-only; never an MCP tool. The decision seals the latest adoption and the gap plans, is
///     acknowledged with 202 and is recorded by <c>ReadinessDecisionProcess</c> once every gap plan is held.
/// </summary>
[Discriminator("bdgrz.readiness.type_i_entry.decide", 2)]
public sealed record DecideTypeIEntry(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    long ExpectedRevision, string Outcome, string Rationale,
    IReadOnlyList<Uuid>? AcknowledgedGapIds = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
