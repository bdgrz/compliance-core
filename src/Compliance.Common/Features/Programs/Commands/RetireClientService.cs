using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.client-service.retire", 1)]
public sealed record RetireClientService(Uuid CommandId, Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Rationale) : IRequest, IClientServiceProgramResourceRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
