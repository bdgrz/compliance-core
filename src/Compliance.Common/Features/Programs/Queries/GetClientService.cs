using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.client-service.get", 1)]
public sealed record GetClientService(Uuid TenantId, Uuid ServiceId,
    long? MinimumRevision = null)
    : IRequest<ClientServiceView>, IClientServiceProgramReadRequest, ICallable;
