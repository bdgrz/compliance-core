using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>Submits the exact current draft version of a response; <paramref name="Version" /> is business content.</summary>
[Discriminator("bdgrz.auditor-requests.submit-audit-response", 2)]
public sealed record SubmitAuditResponse(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid ResponseId, long Version, string SubmissionNote, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
