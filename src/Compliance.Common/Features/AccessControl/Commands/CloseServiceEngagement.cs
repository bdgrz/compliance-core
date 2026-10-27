using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Closes one open service engagement at its current revision.</summary>
[Discriminator("bdgrz.service-engagement.close", 1)]
public sealed record CloseServiceEngagement(Uuid CommandId, Uuid TenantId, Uuid EngagementId, long ExpectedRevision,
    string Reason)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
