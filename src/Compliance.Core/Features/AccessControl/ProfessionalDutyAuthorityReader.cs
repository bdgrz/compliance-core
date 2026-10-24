using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Resolves professional authority only from explicit current duty evidence on the designation owner of the
///     subject, duty and scope, and the active canonical directory. Nothing is read from a firm-wide catalog.
/// </summary>
sealed class ProfessionalDutyAuthorityReader(IAggregateReader reader, FirmStaffViews staffViews,
    IPlatformUserDirectoryReader users, IFirmProfessionalDutyAssertionVerifier assertions,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<CurrentProfessionalDuty?> ReadCurrentAsync(Uuid userId, string duty,
        Uuid? tenantId, CancellationToken ct)
    {
        if (userId == Uuid.Empty || !ProfessionalDutyRules.IsSupportedDutyScope(duty, tenantId) ||
            !await users.ExistsAsync(userId, ct).ConfigureAwait(false))
            return null;
        if (await staffViews.FindByUserAsync(userId, ct).ConfigureAwait(false) is not { IsActive: true } staff)
            return null;
        var owner = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId, duty, tenantId), ct)
            .ConfigureAwait(false);
        if (owner.LatestContent is not { } latest)
            return null;
        FirmProfessionalDutyDesignationView designation;
        try
        {
            designation = await ProfessionalDutyContent.ReadDesignationAsync(content, latest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (!designation.IsActive || designation.AuthorityStatus != "active" || designation.UserId != userId ||
            designation.Duty != duty || designation.TenantId != tenantId || designation.DesignatedAt > now ||
            designation.NotBefore > now || designation.ExpiresAt <= now ||
            designation.StaffMemberId != staff.StaffMemberId || designation.DirectoryStaffRevision != staff.Revision)
            return null;
        if (!assertions.TryVerify(owner.LatestSignedAssertion, now, out var evidence) || evidence.Status != "active" ||
            evidence.AssertionId != designation.AssertionId || evidence.DesignationId != designation.DesignationId ||
            evidence.UserId != designation.UserId || evidence.Duty != designation.Duty ||
            evidence.TenantId != designation.TenantId || evidence.Issuer != designation.AuthorityIssuer ||
            evidence.AuthorityRevision != designation.AuthorityRevision ||
            evidence.SupersedesAssertionId != designation.SupersedesAssertionId ||
            evidence.IssuedAt != designation.AssertionIssuedAt || evidence.NotBefore != designation.NotBefore ||
            evidence.ExpiresAt != designation.ExpiresAt || evidence.EvidenceDigest != designation.EvidenceDigest)
            return null;
        return designation.ContinuityStartRevision is > 0 and var revision && revision <= designation.Revision
            ? new CurrentProfessionalDuty(staff, designation, revision)
            : null;
    }
}
