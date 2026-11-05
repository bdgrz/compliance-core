using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record TenantMembershipSummary(Uuid TenantId, string Name, string Slug,
    string Status = "active", Uuid MembershipEpisodeId = default, Uuid PrimaryDesignationId = default)
{
    public bool IsPrimary => PrimaryDesignationId != Uuid.Empty;
}
