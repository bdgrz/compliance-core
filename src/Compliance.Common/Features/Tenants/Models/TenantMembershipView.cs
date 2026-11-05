using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     A tenant membership. <c>VerifiedEmailAddress</c> is the member's first verified platform email,
///     filled in when the membership is read; the membership projection never stores it.
/// </summary>
public sealed record TenantMembershipView(Uuid UserId, Uuid TenantId,
    string Affiliation = "client_personnel", bool IsSuspended = false,
    DateTimeOffset? SuspendedAt = null, Uuid SuspendedByMemberId = default,
    string? SuspendedByDisplay = null, string? SuspensionReason = null,
    DateTimeOffset? ReinstatedAt = null, Uuid ReinstatedByMemberId = default,
    string? ReinstatedByDisplay = null, string? VerifiedEmailAddress = null,
    string? DisplayName = null, bool IsDeprovisioned = false,
    DateTimeOffset? DeprovisionedAt = null, Uuid DeprovisionedByMemberId = default,
    string? DeprovisionedByDisplay = null, string? DeprovisionReason = null,
    DateOnly? MembershipExpiresOn = null, Uuid MembershipEpisodeId = default,
    IReadOnlyList<Uuid>? ProgramIds = null, Uuid PrimaryDesignationId = default)
{
    /// <summary>The member remains protected until its exact primary mark is removed.</summary>
    public bool IsPrimary => PrimaryDesignationId != Uuid.Empty;

    public bool IsActiveOn(DateOnly utcDate) => !IsSuspended && !IsDeprovisioned &&
        (MembershipExpiresOn is not { } expiresOn || utcDate <= expiresOn);
}
