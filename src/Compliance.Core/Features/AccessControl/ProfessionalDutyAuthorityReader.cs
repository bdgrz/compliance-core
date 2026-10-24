using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Resolves professional authority only from explicit current duty evidence and the active canonical directory.</summary>
sealed class ProfessionalDutyAuthorityReader(IAggregateReader reader, FirmStaffViews staffViews,
    IPlatformUserDirectoryReader users, IFirmProfessionalDutyAssertionVerifier assertions, TimeProvider clock)
{
    public async ValueTask<CurrentProfessionalDuty?> ReadCurrentAsync(Uuid userId, string duty,
        Uuid? tenantId, CancellationToken ct)
    {
        if (userId == Uuid.Empty || !await users.ExistsAsync(userId, ct).ConfigureAwait(false))
            return null;
        if (await staffViews.FindByUserAsync(userId, ct).ConfigureAwait(false) is not { IsActive: true } staff)
            return null;
        var catalog = await reader.HydrateAsync(new FirmProfessionalDutyCatalog(), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (catalog.ActiveDesignation(userId, duty, tenantId, now) is not { } designation ||
            designation.StaffMemberId != staff.StaffMemberId ||
            designation.DirectoryStaffRevision != staff.Revision || !designation.IsActive)
            return null;
        var signedAssertion = catalog.SignedAssertion(designation.DesignationId);
        if (!assertions.TryVerify(signedAssertion, now, out var evidence) || evidence.Status != "active" ||
            evidence.AssertionId != designation.AssertionId || evidence.DesignationId != designation.DesignationId ||
            evidence.UserId != designation.UserId || evidence.Duty != designation.Duty ||
            evidence.TenantId != designation.TenantId || evidence.Issuer != designation.AuthorityIssuer ||
            evidence.AuthorityRevision != designation.AuthorityRevision ||
            evidence.SupersedesAssertionId != designation.SupersedesAssertionId ||
            evidence.IssuedAt != designation.AssertionIssuedAt || evidence.NotBefore != designation.NotBefore ||
            evidence.ExpiresAt != designation.ExpiresAt || evidence.EvidenceDigest != designation.EvidenceDigest)
            return null;
        var continuityStart = catalog.ActiveContinuityStartRevision(designation.DesignationId);
        return continuityStart is { } revision && revision > 0 && revision <= designation.Revision
            ? new CurrentProfessionalDuty(staff, designation, revision)
            : null;
    }
}
