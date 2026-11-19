using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Checks the exact current member mark and completed seat for entry-point staff policy.</summary>
static class PrimaryOrgAdminAuthority
{
    public static async ValueTask<bool> IsCurrentAsync(IAggregateReader? reader, Uuid tenantId, Uuid userId,
        CancellationToken ct)
    {
        if (reader is null || tenantId == Uuid.Empty || userId == Uuid.Empty)
            return false;
        var member = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned || member.Affiliation == "guest" ||
            !member.IsPrimary)
            return false;
        var primaries = await reader.HydrateAsync(new PrimaryOrgAdmins(tenantId), ct).ConfigureAwait(false);
        return primaries.HasDesignated(userId, member.MembershipEpisodeId, member.PrimaryDesignationId);
    }
}
