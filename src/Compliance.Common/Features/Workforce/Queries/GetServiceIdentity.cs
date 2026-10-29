using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.service-identity.get", 1)]
public sealed record GetServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId,
    long? MinimumRevision = null) : IRequest<ServiceIdentityView>, IWorkforceRequest, ICallable;
