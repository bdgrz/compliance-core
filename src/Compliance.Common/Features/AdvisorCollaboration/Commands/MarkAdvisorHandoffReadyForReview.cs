using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Opens the exact selected versions of a draft handoff to the assigned advisor at the expected handoff revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.mark-advisor-handoff-ready-for-review", 2)]
public sealed record MarkAdvisorHandoffReadyForReview(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    long ExpectedRevision) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest,
    IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
