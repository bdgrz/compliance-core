using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.client-service.get", 1)]
public sealed record GetClientService(Uuid TenantId, Uuid ServiceId,
    long? MinimumRevision = null)
    : IRequest<ClientServiceView>, IClientServiceProgramReadRequest, ICallable;
