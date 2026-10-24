using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Amends the open draft of one service engagement at its current revision.</summary>
[Discriminator("bdgrz.service-engagement.amend", 1)]
public sealed record AmendServiceEngagement(Uuid CommandId, Uuid TenantId, Uuid EngagementId, long ExpectedRevision,
    ServiceEngagementDraftContent Content)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
