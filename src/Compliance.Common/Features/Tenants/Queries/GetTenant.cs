using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.tenant.get", 1)]
public sealed record GetTenant(Uuid TenantId) : IRequest<TenantView>, ICallable;
