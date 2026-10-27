using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Cancels an open request with a rationale. <paramref name="ExpectedRevision" /> is the request's revision.</summary>
[Discriminator("bdgrz.evidence.request.cancel", 2)]
public sealed record CancelEvidenceRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long ExpectedRevision, string Rationale)
    : IRequest<EvidenceRequestView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
