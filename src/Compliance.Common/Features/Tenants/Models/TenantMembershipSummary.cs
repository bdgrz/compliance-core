using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record TenantMembershipSummary(Uuid TenantId, string Name, string Slug,
    string Status = "active");
