using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.system_instance.get", 1)]
public sealed record GetSystemInstance(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
    long? MinimumApplicationRevision = null, long? MinimumInstanceRevision = null)
    : IRequest<SystemInstanceView>, IRestrictedApplicationResourceReadRequest, ICallable;
