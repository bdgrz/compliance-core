using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Reopens a resolved request. The request generation advances; every historical response stays part of the
///     next resolution universe and the old resolution can never close the new generation.
/// </summary>
[Discriminator("bdgrz.auditor-requests.reopen-auditor-request", 2)]
public sealed record ReopenAuditorRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    string ReopenReason, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
