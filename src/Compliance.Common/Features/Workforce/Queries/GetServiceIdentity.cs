using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.service-identity.get", 1)]
public sealed record GetServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId,
    long? MinimumRevision = null) : IRequest<ServiceIdentityView>, IWorkforceRequest, ICallable;
