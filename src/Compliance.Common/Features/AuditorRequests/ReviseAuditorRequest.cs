using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Revises the wording, scope, priority and due date of an open request at its owner revision.</summary>
[Discriminator("bdgrz.auditor-requests.revise-auditor-request", 2)]
public sealed record ReviseAuditorRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    long ExpectedRevision, string Wording, string Scope, string Priority, DateOnly DueOn, string ChangeReason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
