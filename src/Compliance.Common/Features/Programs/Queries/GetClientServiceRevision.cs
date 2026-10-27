using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.client-service.revision.get", 1)]
public sealed record GetClientServiceRevision(Uuid TenantId, Uuid ServiceId, long Revision)
    : IRequest<ClientServiceRevisionView>, IClientServiceProgramReadRequest, ICallable;
