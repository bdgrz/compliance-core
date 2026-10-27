using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Admits one response draft on the open request. <paramref name="ResponseId" /> is the logical operation and the
///     response identity; <paramref name="ExpectedRequestRevision" /> is the revision of the request, never of the new
///     response. The complete draft is frozen on the request before the 202 acknowledgment and the response owner is
///     materialized independently.
/// </summary>
[Discriminator("bdgrz.auditor-requests.create-audit-response-draft", 2)]
public sealed record CreateAuditResponseDraft(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId,
    string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds, long ExpectedRequestRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ResponseId;
}
