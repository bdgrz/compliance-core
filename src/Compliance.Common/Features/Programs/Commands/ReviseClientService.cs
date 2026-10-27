using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.client-service.revise", 1)]
public sealed record ReviseClientService(Uuid CommandId, Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Name, string Purpose, string OwnerReference)
    : IRequest, IClientServiceProgramResourceRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
