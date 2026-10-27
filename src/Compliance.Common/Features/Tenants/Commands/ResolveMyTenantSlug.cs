using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.tenant-slug.resolve-mine", 1)]
public sealed record ResolveMyTenantSlug(string Slug) : IRequest<TenantSlugResolution>, ICallable;
