using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.client-service.create", 2)]
public sealed record CreateClientService(Uuid ServiceId, Uuid TenantId, Uuid ProgramId, string Name, string Purpose,
    string OwnerReference) : IRequest<ClientServiceRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ServiceId;
}
