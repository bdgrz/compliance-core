using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.tenant.get", 1)]
public sealed record GetTenant(Uuid TenantId) : IRequest<TenantView>, ICallable;
