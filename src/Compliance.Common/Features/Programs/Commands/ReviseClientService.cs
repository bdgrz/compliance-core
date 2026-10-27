using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.revise", 1)]
public sealed record ReviseClientService(Uuid CommandId, Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Name, string Purpose, string OwnerReference)
    : IRequest, IClientServiceProgramResourceRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
