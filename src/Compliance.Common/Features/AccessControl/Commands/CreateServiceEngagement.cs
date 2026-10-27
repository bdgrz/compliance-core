using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Creates the draft of one service engagement on its own owner. <paramref name="EngagementId" /> is the logical
///     operation identity and <paramref name="ExpectedRevision" /> is the revision of the new owner, which is zero.
/// </summary>
[Discriminator("bdgrz.service-engagement.create", 1)]
public sealed record CreateServiceEngagement(Uuid TenantId, Uuid EngagementId, long ExpectedRevision,
    ServiceEngagementDraftContent Content)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => EngagementId;
}
