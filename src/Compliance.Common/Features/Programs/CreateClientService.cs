using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.create", 2)]
public sealed record CreateClientService(Uuid ServiceId, Uuid TenantId, Uuid ProgramId, string Name, string Purpose,
    string OwnerReference) : IRequest<ClientServiceRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ServiceId;
}
