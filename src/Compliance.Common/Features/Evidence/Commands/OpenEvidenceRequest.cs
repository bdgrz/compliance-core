using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>
///     Asks a member for one piece of evidence by a due date, optionally for one control. The caller chooses
///     <paramref name="EvidenceRequestId" />; it is the request identity and the logical operation, so an exact retry
///     returns the original request and a different intent under the same id conflicts.
/// </summary>
[Discriminator("bdgrz.evidence.request.open", 2)]
public sealed record OpenEvidenceRequest(Uuid EvidenceRequestId, Uuid TenantId, Uuid ProgramId, string Title,
    string Instructions, Uuid OwnerMemberId, DateOnly DueOn, Uuid? ControlId = null)
    : IRequest<EvidenceRequestView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => EvidenceRequestId;
}
