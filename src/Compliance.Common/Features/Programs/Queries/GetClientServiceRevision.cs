using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.client-service.revision.get", 1)]
public sealed record GetClientServiceRevision(Uuid TenantId, Uuid ServiceId, long Revision)
    : IRequest<ClientServiceRevisionView>, IClientServiceProgramReadRequest, ICallable;
