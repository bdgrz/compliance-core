using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>Records the independent disclosure review of the exact submitted response version.</summary>
[Discriminator("bdgrz.auditor-requests.review-audit-response", 2)]
public sealed record ReviewAuditResponse(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid ResponseId, long Version, string Outcome, string Rationale, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
