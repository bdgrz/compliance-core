using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record TenantSlugResolution(Uuid TenantId, string CurrentSlug, bool Redirect);
