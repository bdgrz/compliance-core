using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record TenantRegistration(Uuid TenantId, string Slug);
